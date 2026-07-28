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
    string VatCode, string? IceCode, decimal IceValue,
    string? SupplierCode, string? SupplierAuxCode, decimal Discount, decimal LineSubtotal,
    string TaxCode, decimal VatPercentage, decimal TaxValue, decimal TotalLine);

/// <summary>
/// Un &lt;detalle&gt; del XML que no pudo interpretarse — no aborta el resto del comprobante (ver
/// <see cref="PurchaseXmlDraftParser.Parse"/>). Mismo patrón que <c>PurchaseReceptionParseError</c>
/// usa para el parser TXT: una línea defectuosa se registra, nunca descarta las demás.
/// </summary>
public sealed record ParsedPurchaseXmlLineError(int LineIndex, string? SupplierCode, string? Description, string Reason);

/// <summary>Cabecera + detalle de un comprobante &lt;factura&gt; extraído únicamente de su XML autorizado.</summary>
public sealed record ParsedPurchaseXml(
    string SupplierRuc, string SupplierName,
    string DocTypeCode, string InvoiceNumber, DateOnly IssueDate,
    string? SriPaymentMethodCode,
    IReadOnlyList<ParsedPurchaseXmlLine> Lines,
    IReadOnlyList<ParsedPurchaseXmlLineError> LineErrors);

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
///
/// Resiliencia por línea: la cabecera fiscal (RUC, fecha, secuencial) es obligatoria — sin eso no
/// hay comprobante que armar y <see cref="Parse"/> falla completo. El detalle, en cambio, se
/// interpreta &lt;detalle&gt; por &lt;detalle&gt; de forma independiente — una línea con estructura
/// inesperada (sin impuestos, sin IVA código "2", cantidades inválidas) se reporta en
/// <see cref="ParsedPurchaseXml.LineErrors"/> sin descartar el resto del comprobante. Nunca se
/// fabrica un valor tributario por defecto para una línea defectuosa (Infraestructura CLOSED —
/// Configuración Tributaria: "nunca inventar valores") — un producto legítimamente exento ya trae
/// su propio bloque &lt;impuesto codigo="2"&gt; con tarifa/valor en 0, así que ese caso nunca cae en
/// <see cref="ParsedPurchaseXmlLineError"/>.
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

            var supplierRuc = RequireText(infoTributaria, "ruc");
            var supplierName = RequireText(infoTributaria, "razonSocial");
            var docTypeCode = RequireText(infoTributaria, "codDoc");
            var invoiceNumber = BuildInvoiceNumber(infoTributaria);
            var issueDate = ParseDate(RequireText(infoFactura, "fechaEmision"));
            var sriPaymentMethodCode = infoFactura.Element("pagos")?.Elements("pago")
                .Select(p => OptionalText(p, "formaPago")).FirstOrDefault(v => v is not null);

            var lines = new List<ParsedPurchaseXmlLine>();
            var lineErrors = new List<ParsedPurchaseXmlLineError>();
            var index = 0;
            foreach (var detalle in detalles.Elements("detalle"))
            {
                index++;
                try
                {
                    lines.Add(ParseLine(detalle));
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException)
                {
                    lineErrors.Add(new ParsedPurchaseXmlLineError(
                        index,
                        OptionalText(detalle, "codigoPrincipal") ?? OptionalText(detalle, "codigoAuxiliar"),
                        OptionalText(detalle, "descripcion"),
                        ex.Message));
                }
            }

            var header = new ParsedPurchaseXml(
                SupplierRuc: supplierRuc, SupplierName: supplierName, DocTypeCode: docTypeCode,
                InvoiceNumber: invoiceNumber, IssueDate: issueDate, SriPaymentMethodCode: sriPaymentMethodCode,
                Lines: lines, LineErrors: lineErrors);

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
            ?? throw new FormatException("La línea no tiene impuestos.");

        var vat = impuestos.FirstOrDefault(i => RequireText(i, "codigo") == SriVatTaxCode)
            ?? throw new FormatException("La línea no tiene impuesto IVA.");
        var vatCode = RequireText(vat, "codigoPorcentaje");
        var taxCode = RequireText(vat, "codigo");
        var vatPercentage = ParseDecimal(RequireText(vat, "tarifa"));
        var taxValue = ParseDecimal(RequireText(vat, "valor"));

        var ice = impuestos.FirstOrDefault(i => RequireText(i, "codigo") == SriIceTaxCode);
        var iceCode = ice is not null ? OptionalText(ice, "codigoPorcentaje") : null;
        var iceValue = ice is not null ? ParseDecimal(RequireText(ice, "valor")) : 0m;

        var quantity = ParseDecimal(RequireText(detalle, "cantidad"));
        var unitPrice = ParseDecimal(RequireText(detalle, "precioUnitario"));
        var discount = ParseDecimal(RequireText(detalle, "descuento"));
        var lineSubtotal = ParseDecimal(RequireText(detalle, "precioTotalSinImpuesto"));
        var gross = quantity * unitPrice;
        var discountPct = gross > 0 ? Math.Round(discount / gross * 100, 2, MidpointRounding.AwayFromZero) : 0;
        var totalLine = lineSubtotal + impuestos.Sum(i => ParseDecimal(RequireText(i, "valor")));

        // El esquema SRI admite codigoPrincipal o codigoAuxiliar indistintamente — no es un dato
        // tributario, solo el identificador de producto usado luego por Item Matching.
        var supplierCode = OptionalText(detalle, "codigoPrincipal") ?? OptionalText(detalle, "codigoAuxiliar");

        return new ParsedPurchaseXmlLine(
            Description: RequireText(detalle, "descripcion"),
            Quantity: quantity,
            UnitPrice: unitPrice,
            DiscountPct: Math.Min(100, Math.Max(0, discountPct)),
            VatCode: vatCode,
            IceCode: iceCode,
            IceValue: iceValue,
            SupplierCode: supplierCode,
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
