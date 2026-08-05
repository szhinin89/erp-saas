using System.Globalization;
using System.Xml.Linq;
using ERP.Application.Common;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Parsers;

/// <summary>
/// XML autorizado de Nota de Crédito → <see cref="RideModel"/> (ADR-031 addendum, Fase 12 de
/// P0-01). Consume exclusivamente el string de XML — nunca EF Core, SQL, ni DTOs de Sales/
/// SalesReturn. Todo dato del modelo proviene del XML; nada se completa desde base de datos.
///
/// Forma verificada contra <c>CreditNoteXmlBuilder</c> (ElectronicDocuments) — mismo esquema
/// "notaCredito" v1.1.0 que ese módulo escribe. Diferencias estructurales reales frente a Factura
/// (no simplificaciones de este parser, sino ausencias del propio esquema SRI, verificadas contra
/// <c>NotaCredito_V1.1.0.xsd</c>):
/// <list type="bullet">
/// <item><description>No tiene <c>totalDescuento</c> ni <c>propina</c> a nivel de documento — se
/// completan en <c>0</c> en <see cref="RideHeader"/>, no son un dato omitido sino un concepto que
/// esta versión del comprobante no define.</description></item>
/// <item><description>No tiene sección <c>pagos</c> — <see cref="RideModel.Payments"/> queda
/// siempre vacía.</description></item>
/// <item><description>No tiene <c>direccionComprador</c> — <see cref="RideParty.Address"/> del
/// receptor queda siempre <see langword="null"/>.</description></item>
/// <item><description><c>codigoInterno</c> (equivalente a <c>codigoPrincipal</c> de Factura) es
/// opcional en el esquema — si el emisor no lo incluyó, se usa <c>"-"</c> como marcador visual de
/// "sin código" (mismo criterio que <c>LinesSection</c> ya aplica a columnas no modeladas: nunca
/// se inventa un código real).</description></item>
/// <item><description>El esquema no define ningún campo para el número de autorización/clave de
/// acceso del comprobante modificado — <see cref="RideModifiedDocumentReference"/> nunca lo
/// expone, mismo criterio que <see cref="RideHeader.AuthorizationDate"/>.</description></item>
/// </list>
/// </summary>
public sealed class CreditNoteRideXmlParser : IRideXmlParser
{
    private const string NoCodePlaceholder = "-";

    public RideDocumentType DocumentType => RideDocumentType.CreditNote;

    public Result<RideModel> Parse(string authorizedXml)
    {
        try
        {
            var notaCredito =
                XDocument.Parse(authorizedXml).Root
                ?? throw new FormatException("El XML no tiene un elemento raíz.");

            var infoTributaria = RequireElement(notaCredito, "infoTributaria");
            var infoNotaCredito = RequireElement(notaCredito, "infoNotaCredito");
            var detalles = RequireElement(notaCredito, "detalles");

            var accessKey = RideAccessKey.Create(RequireText(infoTributaria, "claveAcceso"));

            var modifiedDocument = RideModifiedDocumentReference.Create(
                documentTypeCode: RequireText(infoNotaCredito, "codDocModificado"),
                number: RequireText(infoNotaCredito, "numDocModificado"),
                issueDate: ParseDate(RequireText(infoNotaCredito, "fechaEmisionDocSustento"))
            );

            var header = RideHeader.Create(
                environment: RequireText(infoTributaria, "ambiente"),
                emissionType: RequireText(infoTributaria, "tipoEmision"),
                documentTypeCode: RequireText(infoTributaria, "codDoc"),
                establishment: RequireText(infoTributaria, "estab"),
                emissionPoint: RequireText(infoTributaria, "ptoEmi"),
                sequential: RequireText(infoTributaria, "secuencial"),
                establishmentAddress: RequireText(infoNotaCredito, "dirEstablecimiento"),
                issueDate: ParseDate(RequireText(infoNotaCredito, "fechaEmision")),
                currencyCode: RequireText(infoNotaCredito, "moneda"),
                accessKey: accessKey,
                authorizationNumber: accessKey.Value,
                authorizationDate: null,
                subtotalWithoutTax: ParseDecimal(RequireText(infoNotaCredito, "totalSinImpuestos")),
                totalDiscount: 0m,
                tip: 0m,
                grandTotal: ParseDecimal(RequireText(infoNotaCredito, "valorModificacion")),
                reason: RequireText(infoNotaCredito, "motivo"),
                modifiedDocument: modifiedDocument
            );

            var issuer = RideParty.Create(
                identificationType: null,
                identificationNumber: RequireText(infoTributaria, "ruc"),
                legalName: RequireText(infoTributaria, "razonSocial"),
                tradeName: OptionalText(infoTributaria, "nombreComercial"),
                address: OptionalText(infoTributaria, "dirMatriz"),
                isAccountingRequired: RequireText(infoNotaCredito, "obligadoContabilidad")
                    .Equals("SI", StringComparison.OrdinalIgnoreCase),
                taxRegime: OptionalText(infoTributaria, "contribuyenteRimpe")
            );

            var receiver = RideParty.Create(
                identificationType: RequireText(infoNotaCredito, "tipoIdentificacionComprador"),
                identificationNumber: RequireText(infoNotaCredito, "identificacionComprador"),
                legalName: RequireText(infoNotaCredito, "razonSocialComprador"),
                address: null
            );

            var lines = detalles.Elements("detalle").Select(ParseLine).ToList();

            var taxSummary =
                infoNotaCredito
                    .Element("totalConImpuestos")
                    ?.Elements("totalImpuesto")
                    .Select(ParseDocumentTax)
                    .ToList()
                ?? [];

            var additionalInfo =
                notaCredito
                    .Element("infoAdicional")
                    ?.Elements("campoAdicional")
                    .Select(ParseAdditionalField)
                    .ToList()
                ?? [];

            var model = RideModel.Create(
                header,
                issuer,
                receiver,
                lines,
                taxSummary,
                payments: [],
                additionalInfo
            );
            return Result<RideModel>.Success(model);
        }
        catch (Exception ex)
            when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            return Result<RideModel>.ValidationFailure(
                $"El XML autorizado de nota de crédito no se pudo interpretar: {ex.Message}"
            );
        }
    }

    private static RideLine ParseLine(XElement detalle)
    {
        var taxes =
            detalle.Element("impuestos")?.Elements("impuesto").Select(ParseLineTax).ToList()
            ?? throw new FormatException("Una línea de detalle no tiene impuestos.");

        return RideLine.Create(
            code: OptionalText(detalle, "codigoInterno") ?? NoCodePlaceholder,
            description: RequireText(detalle, "descripcion"),
            quantity: ParseDecimal(RequireText(detalle, "cantidad")),
            unitPrice: ParseDecimal(RequireText(detalle, "precioUnitario")),
            discount: ParseDecimal(RequireText(detalle, "descuento")),
            subtotal: ParseDecimal(RequireText(detalle, "precioTotalSinImpuesto")),
            taxes: taxes
        );
    }

    private static RideTaxSummary ParseLineTax(XElement impuesto) =>
        RideTaxSummary.Create(
            taxCode: RequireText(impuesto, "codigo"),
            taxPercentageCode: RequireText(impuesto, "codigoPorcentaje"),
            taxableBase: ParseDecimal(RequireText(impuesto, "baseImponible")),
            taxAmount: ParseDecimal(RequireText(impuesto, "valor")),
            rate: ParseDecimal(RequireText(impuesto, "tarifa"))
        );

    private static RideTaxSummary ParseDocumentTax(XElement totalImpuesto) =>
        RideTaxSummary.Create(
            taxCode: RequireText(totalImpuesto, "codigo"),
            taxPercentageCode: RequireText(totalImpuesto, "codigoPorcentaje"),
            taxableBase: ParseDecimal(RequireText(totalImpuesto, "baseImponible")),
            taxAmount: ParseDecimal(RequireText(totalImpuesto, "valor"))
        );

    private static RideAdditionalInfo ParseAdditionalField(XElement campoAdicional) =>
        RideAdditionalInfo.Create(
            name: campoAdicional.Attribute("nombre")?.Value
                ?? throw new FormatException("Un campo adicional no tiene el atributo 'nombre'."),
            value: campoAdicional.Value
        );

    private static XElement RequireElement(XElement parent, string name) =>
        parent.Element(name)
        ?? throw new FormatException($"Falta el elemento obligatorio '{name}'.");

    private static string RequireText(XElement parent, string name) =>
        parent.Element(name)?.Value is { Length: > 0 } value
            ? value
            : throw new FormatException($"Falta el elemento obligatorio '{name}'.");

    private static string? OptionalText(XElement parent, string name) =>
        parent.Element(name)?.Value is { Length: > 0 } value ? value : null;

    private static decimal ParseDecimal(string text) =>
        decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static DateOnly ParseDate(string text) =>
        DateOnly.ParseExact(text, "dd/MM/yyyy", CultureInfo.InvariantCulture);
}
