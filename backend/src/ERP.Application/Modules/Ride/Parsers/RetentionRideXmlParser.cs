using ERP.Application.Common;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;
using System.Globalization;
using System.Xml.Linq;

namespace ERP.Application.Modules.Ride.Parsers;

/// <summary>
/// XML autorizado de Comprobante de Retención → <see cref="RetentionRideModel"/>
/// (RETENTIONS-RIDE-TEMPLATE-03C). Consume exclusivamente el string de XML — nunca EF Core, SQL,
/// ni <c>RetentionDocument</c>/repositorios de Retentions. Todo dato del modelo proviene del XML;
/// nada se completa desde base de datos ni se recalcula (bases/porcentajes/valores se transcriben
/// tal cual).
///
/// Forma verificada contra <c>RetentionXmlBuilder</c> (ElectronicDocuments,
/// RETENTIONS-SRI-XML-MAPPER-03B) — mismo esquema "comprobanteRetencion" v1.0.0 que ese módulo
/// escribe. Por eso:
/// <list type="bullet">
/// <item><description><c>codSustento</c> no se parsea — el esquema 1.0.0 no lo define (ver
/// comentario de <c>RetentionXmlBuilder</c> sobre la elección de versión); nunca se inventa.</description></item>
/// <item><description><c>codDocSustento</c>/<c>numDocSustento</c>/<c>fechaEmisionDocSustento</c>
/// son opcionales (minOccurs=0 en el XSD) y se leen del primer <c>&lt;impuesto&gt;</c> — el mismo
/// documento sustento se repite idéntico en cada línea por construcción del builder, nunca varía
/// entre líneas de un mismo comprobante.</description></item>
/// <item><description>No hay <c>totalSinImpuestos</c>/<c>importeTotal</c> del documento sustento
/// en este esquema — <see cref="RetentionRideModel.TotalRetained"/> es la suma de
/// <c>valorRetenido</c> de las líneas, un cálculo visual sobre el propio XML, no un dato del
/// documento sustento ni una consulta al dominio.</description></item>
/// <item><description>El número de autorización se deriva de la clave de acceso (regla AUTH-01,
/// mismo criterio que <c>InvoiceRideXmlParser</c>/<c>CreditNoteRideXmlParser</c>); la fecha de
/// autorización queda <see langword="null"/> porque no viaja dentro de <c>comprobanteRetencion</c>
/// — nunca se inventa si el comprobante todavía no está autorizado.</description></item>
/// </list>
/// </summary>
public sealed class RetentionRideXmlParser : IRetentionRideXmlParser
{
    public RideDocumentType DocumentType => RideDocumentType.Retention;

    public Result<RetentionRideModel> Parse(string authorizedXml)
    {
        try
        {
            var comprobanteRetencion =
                XDocument.Parse(authorizedXml).Root
                ?? throw new FormatException("El XML no tiene un elemento raíz.");

            var infoTributaria = RequireElement(comprobanteRetencion, "infoTributaria");
            var infoCompRetencion = RequireElement(comprobanteRetencion, "infoCompRetencion");
            var impuestos = RequireElement(comprobanteRetencion, "impuestos");

            var accessKey = RideAccessKey.Create(RequireText(infoTributaria, "claveAcceso"));

            var header = RetentionRideHeader.Create(
                environment: RequireText(infoTributaria, "ambiente"),
                emissionType: RequireText(infoTributaria, "tipoEmision"),
                documentTypeCode: RequireText(infoTributaria, "codDoc"),
                establishment: RequireText(infoTributaria, "estab"),
                emissionPoint: RequireText(infoTributaria, "ptoEmi"),
                sequential: RequireText(infoTributaria, "secuencial"),
                establishmentAddress: RequireText(infoCompRetencion, "dirEstablecimiento"),
                issueDate: ParseDate(RequireText(infoCompRetencion, "fechaEmision")),
                fiscalPeriod: RequireText(infoCompRetencion, "periodoFiscal"),
                accessKey: accessKey,
                authorizationNumber: accessKey.Value,
                authorizationDate: null
            );

            var issuer = RideParty.Create(
                identificationType: null,
                identificationNumber: RequireText(infoTributaria, "ruc"),
                legalName: RequireText(infoTributaria, "razonSocial"),
                tradeName: OptionalText(infoTributaria, "nombreComercial"),
                address: OptionalText(infoTributaria, "dirMatriz"),
                isAccountingRequired: RequireText(infoCompRetencion, "obligadoContabilidad")
                    .Equals("SI", StringComparison.OrdinalIgnoreCase),
                taxRegime: OptionalText(infoTributaria, "contribuyenteRimpe")
            );

            var subjectWithheld = RideParty.Create(
                identificationType: RequireText(infoCompRetencion, "tipoIdentificacionSujetoRetenido"),
                identificationNumber: RequireText(infoCompRetencion, "identificacionSujetoRetenido"),
                legalName: RequireText(infoCompRetencion, "razonSocialSujetoRetenido"),
                address: null
            );

            var impuestoElements = impuestos.Elements("impuesto").ToList();
            var lines = impuestoElements.Select(ParseLine).ToList();
            var sourceDocument = impuestoElements.Count > 0
                ? ParseSourceDocument(impuestoElements[0])
                : RetentionRideSourceDocument.Empty();

            var additionalInfo =
                comprobanteRetencion
                    .Element("infoAdicional")
                    ?.Elements("campoAdicional")
                    .Select(ParseAdditionalField)
                    .ToList()
                ?? [];

            var model = RetentionRideModel.Create(
                header,
                issuer,
                subjectWithheld,
                sourceDocument,
                lines,
                totalRetained: lines.Sum(l => l.RetainedAmount),
                additionalInfo
            );
            return Result<RetentionRideModel>.Success(model);
        }
        catch (Exception ex)
            when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            return Result<RetentionRideModel>.ValidationFailure(
                $"El XML autorizado de comprobante de retención no se pudo interpretar: {ex.Message}"
            );
        }
    }

    private static RetentionRideTaxLine ParseLine(XElement impuesto) =>
        RetentionRideTaxLine.Create(
            taxCode: RequireText(impuesto, "codigo"),
            retentionCode: RequireText(impuesto, "codigoRetencion"),
            baseAmount: ParseDecimal(RequireText(impuesto, "baseImponible")),
            retentionRate: ParseDecimal(RequireText(impuesto, "porcentajeRetener")),
            retainedAmount: ParseDecimal(RequireText(impuesto, "valorRetenido"))
        );

    private static RetentionRideSourceDocument ParseSourceDocument(XElement impuesto)
    {
        var issueDateText = OptionalText(impuesto, "fechaEmisionDocSustento");
        return RetentionRideSourceDocument.Create(
            documentTypeCode: OptionalText(impuesto, "codDocSustento"),
            number: OptionalText(impuesto, "numDocSustento"),
            issueDate: issueDateText is null ? null : ParseDate(issueDateText)
        );
    }

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
