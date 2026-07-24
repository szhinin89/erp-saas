using System.Globalization;
using System.Xml.Linq;
using ERP.Application.Common;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Parsers;

/// <summary>
/// XML autorizado de Factura → <see cref="RideModel"/> (ADR-025 §6, Fase 6). Consume
/// exclusivamente el string de XML — nunca EF Core, SQL, ni DTOs de Sales. Todo dato del modelo
/// proviene del XML; nada se completa desde base de datos.
///
/// Forma verificada contra <c>InvoiceXmlBuilder</c> (ElectronicDocuments, FROZEN) — mismo
/// esquema "factura" v1.1.0 que ese módulo escribe. El XML que realmente llega aquí es el
/// contenido puro de <c>&lt;factura&gt;</c> (ver <c>SriSoapClient.ParseAutorizacionResponse</c>):
/// nunca incluye <c>numeroAutorizacion</c>/<c>fechaAutorizacion</c> (viven en columnas propias de
/// <c>ElectronicDocument</c>, fuera del XML) — <see cref="RideHeader.AuthorizationNumber"/> se
/// deriva de <see cref="RideAccessKey"/> (AUTH-01: en el esquema offline el número de
/// autorización es siempre la propia clave de acceso) y <see cref="RideHeader.AuthorizationDate"/>
/// queda <see langword="null"/> (hueco real declarado en la auditoría de la Fase 6 — Ride v1.0
/// no la muestra en el RIDE todavía).
/// </summary>
public sealed class InvoiceRideXmlParser : IRideXmlParser
{
    public RideDocumentType DocumentType => RideDocumentType.Invoice;

    public Result<RideModel> Parse(string authorizedXml)
    {
        try
        {
            var factura = XDocument.Parse(authorizedXml).Root
                ?? throw new FormatException("El XML no tiene un elemento raíz.");

            var infoTributaria = RequireElement(factura, "infoTributaria");
            var infoFactura = RequireElement(factura, "infoFactura");
            var detalles = RequireElement(factura, "detalles");

            var accessKey = RideAccessKey.Create(RequireText(infoTributaria, "claveAcceso"));

            var header = RideHeader.Create(
                environment: RequireText(infoTributaria, "ambiente"),
                emissionType: RequireText(infoTributaria, "tipoEmision"),
                documentTypeCode: RequireText(infoTributaria, "codDoc"),
                establishment: RequireText(infoTributaria, "estab"),
                emissionPoint: RequireText(infoTributaria, "ptoEmi"),
                sequential: RequireText(infoTributaria, "secuencial"),
                establishmentAddress: RequireText(infoFactura, "dirEstablecimiento"),
                issueDate: ParseDate(RequireText(infoFactura, "fechaEmision")),
                currencyCode: RequireText(infoFactura, "moneda"),
                accessKey: accessKey,
                authorizationNumber: accessKey.Value,
                authorizationDate: null,
                subtotalWithoutTax: ParseDecimal(RequireText(infoFactura, "totalSinImpuestos")),
                totalDiscount: ParseDecimal(RequireText(infoFactura, "totalDescuento")),
                tip: ParseDecimal(RequireText(infoFactura, "propina")),
                grandTotal: ParseDecimal(RequireText(infoFactura, "importeTotal")));

            var issuer = RideParty.Create(
                identificationType: null,
                identificationNumber: RequireText(infoTributaria, "ruc"),
                legalName: RequireText(infoTributaria, "razonSocial"),
                tradeName: OptionalText(infoTributaria, "nombreComercial"),
                address: OptionalText(infoTributaria, "dirMatriz"),
                isAccountingRequired: RequireText(infoFactura, "obligadoContabilidad")
                    .Equals("SI", StringComparison.OrdinalIgnoreCase),
                taxRegime: OptionalText(infoTributaria, "contribuyenteRimpe"));

            var receiver = RideParty.Create(
                identificationType: RequireText(infoFactura, "tipoIdentificacionComprador"),
                identificationNumber: RequireText(infoFactura, "identificacionComprador"),
                legalName: RequireText(infoFactura, "razonSocialComprador"),
                address: OptionalText(infoFactura, "direccionComprador"));

            var lines = detalles.Elements("detalle").Select(ParseLine).ToList();

            var taxSummary = infoFactura.Element("totalConImpuestos")?.Elements("totalImpuesto")
                .Select(ParseDocumentTax).ToList() ?? [];

            var payments = infoFactura.Element("pagos")?.Elements("pago").Select(ParsePayment).ToList() ?? [];

            var additionalInfo = factura.Element("infoAdicional")?.Elements("campoAdicional")
                .Select(ParseAdditionalField).ToList() ?? [];

            var model = RideModel.Create(header, issuer, receiver, lines, taxSummary, payments, additionalInfo);
            return Result<RideModel>.Success(model);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            return Result<RideModel>.ValidationFailure(
                $"El XML autorizado de factura no se pudo interpretar: {ex.Message}");
        }
    }

    private static RideLine ParseLine(XElement detalle)
    {
        var taxes = detalle.Element("impuestos")?.Elements("impuesto").Select(ParseLineTax).ToList()
            ?? throw new FormatException("Una línea de detalle no tiene impuestos.");

        return RideLine.Create(
            code: RequireText(detalle, "codigoPrincipal"),
            description: RequireText(detalle, "descripcion"),
            quantity: ParseDecimal(RequireText(detalle, "cantidad")),
            unitPrice: ParseDecimal(RequireText(detalle, "precioUnitario")),
            discount: ParseDecimal(RequireText(detalle, "descuento")),
            subtotal: ParseDecimal(RequireText(detalle, "precioTotalSinImpuesto")),
            taxes: taxes);
    }

    private static RideTaxSummary ParseLineTax(XElement impuesto) => RideTaxSummary.Create(
        taxCode: RequireText(impuesto, "codigo"),
        taxPercentageCode: RequireText(impuesto, "codigoPorcentaje"),
        taxableBase: ParseDecimal(RequireText(impuesto, "baseImponible")),
        taxAmount: ParseDecimal(RequireText(impuesto, "valor")),
        rate: ParseDecimal(RequireText(impuesto, "tarifa")));

    private static RideTaxSummary ParseDocumentTax(XElement totalImpuesto) => RideTaxSummary.Create(
        taxCode: RequireText(totalImpuesto, "codigo"),
        taxPercentageCode: RequireText(totalImpuesto, "codigoPorcentaje"),
        taxableBase: ParseDecimal(RequireText(totalImpuesto, "baseImponible")),
        taxAmount: ParseDecimal(RequireText(totalImpuesto, "valor")));

    private static RidePaymentInfo ParsePayment(XElement pago)
    {
        var termText = OptionalText(pago, "plazo");
        return RidePaymentInfo.Create(
            paymentMethodCode: RequireText(pago, "formaPago"),
            amount: ParseDecimal(RequireText(pago, "total")),
            term: termText is null ? null : int.Parse(termText, CultureInfo.InvariantCulture),
            timeUnit: OptionalText(pago, "unidadTiempo"));
    }

    private static RideAdditionalInfo ParseAdditionalField(XElement campoAdicional) => RideAdditionalInfo.Create(
        name: campoAdicional.Attribute("nombre")?.Value
            ?? throw new FormatException("Un campo adicional no tiene el atributo 'nombre'."),
        value: campoAdicional.Value);

    private static XElement RequireElement(XElement parent, string name) =>
        parent.Element(name) ?? throw new FormatException($"Falta el elemento obligatorio '{name}'.");

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
