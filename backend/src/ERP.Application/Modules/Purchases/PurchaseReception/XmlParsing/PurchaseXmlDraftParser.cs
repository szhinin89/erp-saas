using System.Globalization;
using System.Xml.Linq;
using ERP.Application.Common;

namespace ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;

/// <summary>
/// Una línea de detalle extraída del XML, con todo el dato disponible del comprobante — sin
/// ItemId/WarehouseId, se resuelven manualmente en el formulario (fase de conciliación de ítems,
/// todavía no implementada). <see cref="VatCode"/>/<see cref="IceCode"/>/<see cref="DiscountPct"/>
/// son los campos que efectivamente se usan para crear la línea de compra; el resto
/// (<see cref="SupplierCode"/> en adelante) es información de solo lectura para que el usuario vea
/// exactamente lo que trae el XML antes de emparejar el producto.
/// </summary>
public sealed record ParsedPurchaseXmlLine(
    string Description, decimal Quantity, decimal UnitPrice, decimal DiscountPct,
    string VatCode, string? IceCode,
    string SupplierCode, string? SupplierAuxCode, decimal Discount, decimal LineSubtotal,
    string TaxCode, decimal VatPercentage, decimal TaxValue, decimal TotalLine);

/// <summary>Cabecera + detalle de un comprobante &lt;factura&gt; extraído únicamente de su XML autorizado.</summary>
public sealed record ParsedPurchaseXml(
    string SupplierRuc, string SupplierName,
    string DocTypeCode, string InvoiceNumber, DateOnly IssueDate,
    string? SriPaymentMethodCode,
    IReadOnlyList<ParsedPurchaseXmlLine> Lines);

public interface IPurchaseXmlDraftParser
{
    Result<ParsedPurchaseXml> Parse(string xmlContent);
}

/// <summary>
/// XML autorizado de Factura (comprobante recibido de un proveedor) → <see cref="ParsedPurchaseXml"/>.
/// Consume exclusivamente el string de XML — nunca EF Core, SQL ni consultas al SRI. Misma forma
/// "factura" v1.1.0 que escribe <c>InvoiceXmlBuilder</c> (ElectronicDocuments, FROZEN) y que ya
/// parsea <c>InvoiceRideXmlParser</c> (Ride) — <c>impuesto/codigo</c> "2" identifica IVA y "3" ICE
/// (ver <c>ISriTaxCategoryCodeResolver</c>), <c>codigoPorcentaje</c> es directamente el VatCode/IceCode
/// del sistema (Infraestructura CLOSED — Configuración Tributaria).
/// </summary>
public sealed class PurchaseXmlDraftParser : IPurchaseXmlDraftParser
{
    private const string SriVatTaxCode = "2";
    private const string SriIceTaxCode = "3";

    public Result<ParsedPurchaseXml> Parse(string xmlContent)
    {
        try
        {
            var factura = XDocument.Parse(xmlContent).Root
                ?? throw new FormatException("El XML no tiene un elemento raíz.");

            var infoTributaria = RequireElement(factura, "infoTributaria");
            var infoFactura = RequireElement(factura, "infoFactura");
            var detalles = RequireElement(factura, "detalles");

            var header = new ParsedPurchaseXml(
                SupplierRuc: RequireText(infoTributaria, "ruc"),
                SupplierName: RequireText(infoTributaria, "razonSocial"),
                DocTypeCode: RequireText(infoTributaria, "codDoc"),
                InvoiceNumber: BuildInvoiceNumber(infoTributaria),
                IssueDate: ParseDate(RequireText(infoFactura, "fechaEmision")),
                SriPaymentMethodCode: infoFactura.Element("pagos")?.Elements("pago")
                    .Select(p => OptionalText(p, "formaPago")).FirstOrDefault(v => v is not null),
                Lines: detalles.Elements("detalle").Select(ParseLine).ToList());

            return Result<ParsedPurchaseXml>.Success(header);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            return Result<ParsedPurchaseXml>.ValidationFailure(
                $"El XML del comprobante no se pudo interpretar: {ex.Message}");
        }
    }

    private static string BuildInvoiceNumber(XElement infoTributaria) => string.Join("-",
        RequireText(infoTributaria, "estab"),
        RequireText(infoTributaria, "ptoEmi"),
        RequireText(infoTributaria, "secuencial"));

    private static ParsedPurchaseXmlLine ParseLine(XElement detalle)
    {
        var impuestos = detalle.Element("impuestos")?.Elements("impuesto").ToList()
            ?? throw new FormatException("Una línea de detalle no tiene impuestos.");

        var vat = impuestos.FirstOrDefault(i => RequireText(i, "codigo") == SriVatTaxCode)
            ?? throw new FormatException("Una línea de detalle no tiene impuesto IVA.");
        var vatCode = RequireText(vat, "codigoPorcentaje");
        var taxCode = RequireText(vat, "codigo");
        var vatPercentage = ParseDecimal(RequireText(vat, "tarifa"));
        var taxValue = ParseDecimal(RequireText(vat, "valor"));

        var iceCode = impuestos.FirstOrDefault(i => RequireText(i, "codigo") == SriIceTaxCode) is { } ice
            ? OptionalText(ice, "codigoPorcentaje")
            : null;

        var quantity = ParseDecimal(RequireText(detalle, "cantidad"));
        var unitPrice = ParseDecimal(RequireText(detalle, "precioUnitario"));
        var discount = ParseDecimal(RequireText(detalle, "descuento"));
        var lineSubtotal = ParseDecimal(RequireText(detalle, "precioTotalSinImpuesto"));
        var gross = quantity * unitPrice;
        var discountPct = gross > 0 ? Math.Round(discount / gross * 100, 2, MidpointRounding.AwayFromZero) : 0;
        var totalLine = lineSubtotal + impuestos.Sum(i => ParseDecimal(RequireText(i, "valor")));

        return new ParsedPurchaseXmlLine(
            Description: RequireText(detalle, "descripcion"),
            Quantity: quantity,
            UnitPrice: unitPrice,
            DiscountPct: Math.Min(100, Math.Max(0, discountPct)),
            VatCode: vatCode,
            IceCode: iceCode,
            SupplierCode: RequireText(detalle, "codigoPrincipal"),
            SupplierAuxCode: OptionalText(detalle, "codigoAuxiliar"),
            Discount: discount,
            LineSubtotal: lineSubtotal,
            TaxCode: taxCode,
            VatPercentage: vatPercentage,
            TaxValue: taxValue,
            TotalLine: totalLine);
    }

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
