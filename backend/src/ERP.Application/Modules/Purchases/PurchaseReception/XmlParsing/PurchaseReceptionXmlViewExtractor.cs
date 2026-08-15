using ERP.Domain.Modules.Purchases;
using System.Globalization;
using System.Xml.Linq;

namespace ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;

/// <summary>Cabecera "extra" del XML que <c>PurchaseReceptionDocument</c> no persiste — únicamente lo que <see cref="PurchaseReceptionXmlViewExtractor"/> lee para la vista de solo lectura (FLOW-READY-02E.1).</summary>
public sealed record PurchaseReceptionXmlViewExtras(
    string? SupplierTradeName,
    string? ReferralGuide,
    string? PaymentMethodCode,
    string? PaymentTerm,
    string? PaymentTimeUnit,
    PurchaseReceptionXmlViewExtraTotals Totals,
    decimal DiscountAmount,
    decimal IceAmount,
    IReadOnlyList<PurchaseReceptionXmlViewExtraTax> TaxSummaries,
    IReadOnlyList<PurchaseReceptionXmlViewExtraLine> Lines,
    string? ModifiedDocumentType,
    DateOnly? ModifiedDocumentDate,
    string? ModificationReason
);

public sealed record PurchaseReceptionXmlViewExtraTotals(
    decimal? TotalWithoutTaxes,
    decimal? TotalDiscount,
    decimal? Tip,
    decimal? TotalAmount
);

public sealed record PurchaseReceptionXmlViewExtraTax(
    string TaxCode,
    string TaxRateCode,
    decimal? TaxRate,
    decimal TaxableBase,
    decimal TaxAmount
);

public sealed record PurchaseReceptionXmlViewExtraLine(
    string? MainCode,
    string? AuxCode,
    string Description,
    IReadOnlyList<PurchaseReceptionXmlViewExtraLineTax> Taxes,
    IReadOnlyList<PurchaseReceptionXmlViewAdditionalDetail> AdditionalDetails
);

public sealed record PurchaseReceptionXmlViewExtraLineTax(
    string TaxCode,
    string TaxRateCode,
    decimal Rate,
    decimal TaxableBase,
    decimal TaxAmount
);

public sealed record PurchaseReceptionXmlViewAdditionalDetail(string Name, string Value);

/// <summary>
/// Extrae de un XML SRI ya autorizado (Factura o Nota de Crédito) únicamente los campos de
/// cabecera que <see cref="Entities.PurchaseReceptionDocument">PurchaseReceptionDocument</see> no
/// persiste — el resto de la vista (montos, líneas) sale de la entidad ya guardada, nunca de este
/// extractor. Puramente de solo lectura: no crea líneas, no aplica Item Matching, no cambia estado,
/// no es el parser de ingesta (<c>PurchaseXmlDraftParser</c>) — ver comentario de esa clase para el
/// motivo de no reutilizarla (no cubre &lt;notaCredito&gt; ni &lt;totalConImpuestos&gt;).
///
/// Nunca inventa un valor: si un elemento opcional del esquema SRI no está presente (p. ej.
/// &lt;tarifa&gt; en &lt;totalImpuesto&gt;, que el esquema no define a ese nivel), el campo
/// correspondiente queda <see langword="null"/>/0, nunca calculado o asumido.
/// </summary>
public static class PurchaseReceptionXmlViewExtractor
{
    private const string SriIceTaxCode = SriTaxCategoryCodes.Ice;

    public static PurchaseReceptionXmlViewExtras Extract(string xmlContent)
    {
        var root = XDocument.Parse(xmlContent).Root;
        if (root is null)
            return Empty;

        return root.Name.LocalName switch
        {
            "factura" => ExtractFromInvoice(root),
            "notaCredito" => ExtractFromCreditNote(root),
            _ => Empty,
        };
    }

    private static readonly PurchaseReceptionXmlViewExtraTotals EmptyTotals = new(null, null, null, null);

    private static readonly PurchaseReceptionXmlViewExtras Empty =
        new(null, null, null, null, null, EmptyTotals, 0m, 0m, [], [], null, null, null);

    private static PurchaseReceptionXmlViewExtras ExtractFromInvoice(XElement factura)
    {
        var infoTributaria = factura.Element("infoTributaria");
        var infoFactura = factura.Element("infoFactura");
        if (infoFactura is null)
            return Empty;

        var taxSummaries = ParseTaxSummaries(infoFactura.Element("totalConImpuestos"));
        var payment = infoFactura.Element("pagos")?.Elements("pago").FirstOrDefault();

        return new PurchaseReceptionXmlViewExtras(
            SupplierTradeName: OptionalText(infoTributaria, "nombreComercial"),
            ReferralGuide: OptionalText(infoFactura, "guiaRemision"),
            PaymentMethodCode: OptionalText(payment, "formaPago"),
            PaymentTerm: OptionalText(payment, "plazo"),
            PaymentTimeUnit: OptionalText(payment, "unidadTiempo"),
            Totals: ParseInvoiceTotals(infoFactura),
            DiscountAmount: OptionalDecimal(infoFactura, "totalDescuento") ?? 0m,
            IceAmount: SumIce(taxSummaries),
            TaxSummaries: taxSummaries,
            Lines: ParseLines(factura.Element("detalles")),
            ModifiedDocumentType: null,
            ModifiedDocumentDate: null,
            ModificationReason: null
        );
    }

    private static PurchaseReceptionXmlViewExtras ExtractFromCreditNote(XElement notaCredito)
    {
        var infoTributaria = notaCredito.Element("infoTributaria");
        var infoNotaCredito = notaCredito.Element("infoNotaCredito");
        if (infoNotaCredito is null)
            return Empty;

        var taxSummaries = ParseTaxSummaries(infoNotaCredito.Element("totalConImpuestos"));

        return new PurchaseReceptionXmlViewExtras(
            SupplierTradeName: OptionalText(infoTributaria, "nombreComercial"),
            ReferralGuide: null,
            PaymentMethodCode: null,
            PaymentTerm: null,
            PaymentTimeUnit: null,
            Totals: new PurchaseReceptionXmlViewExtraTotals(
                OptionalDecimal(infoNotaCredito, "totalSinImpuestos"),
                null,
                null,
                OptionalDecimal(infoNotaCredito, "valorModificacion")
            ),
            // El esquema notaCredito no define totalDescuento a nivel de documento.
            DiscountAmount: 0m,
            IceAmount: SumIce(taxSummaries),
            TaxSummaries: taxSummaries,
            Lines: ParseLines(notaCredito.Element("detalles")),
            ModifiedDocumentType: OptionalText(infoNotaCredito, "codDocModificado"),
            ModifiedDocumentDate: OptionalDate(infoNotaCredito, "fechaEmisionDocSustento"),
            ModificationReason: OptionalText(infoNotaCredito, "motivo")
        );
    }

    private static PurchaseReceptionXmlViewExtraTotals ParseInvoiceTotals(XElement infoFactura) =>
        new(
            OptionalDecimal(infoFactura, "totalSinImpuestos"),
            OptionalDecimal(infoFactura, "totalDescuento"),
            OptionalDecimal(infoFactura, "propina"),
            OptionalDecimal(infoFactura, "importeTotal")
        );

    private static IReadOnlyList<PurchaseReceptionXmlViewExtraTax> ParseTaxSummaries(
        XElement? totalConImpuestos
    ) =>
        totalConImpuestos
            ?.Elements("totalImpuesto")
            .Select(t => new PurchaseReceptionXmlViewExtraTax(
                TaxCode: OptionalText(t, "codigo") ?? string.Empty,
                TaxRateCode: OptionalText(t, "codigoPorcentaje") ?? string.Empty,
                TaxRate: OptionalDecimal(t, "tarifa"),
                TaxableBase: OptionalDecimal(t, "baseImponible") ?? 0m,
                TaxAmount: OptionalDecimal(t, "valor") ?? 0m
            ))
            .ToList()
        ?? [];

    private static IReadOnlyList<PurchaseReceptionXmlViewExtraLine> ParseLines(XElement? detalles) =>
        detalles
            ?.Elements("detalle")
            .Select(d => new PurchaseReceptionXmlViewExtraLine(
                MainCode: OptionalText(d, "codigoPrincipal"),
                AuxCode: OptionalText(d, "codigoAuxiliar"),
                Description: OptionalText(d, "descripcion") ?? string.Empty,
                Taxes: ParseLineTaxes(d.Element("impuestos")),
                AdditionalDetails: ParseAdditionalDetails(d.Element("detallesAdicionales"))
            ))
            .ToList()
        ?? [];

    private static IReadOnlyList<PurchaseReceptionXmlViewExtraLineTax> ParseLineTaxes(
        XElement? impuestos
    ) =>
        impuestos
            ?.Elements("impuesto")
            .Select(t => new PurchaseReceptionXmlViewExtraLineTax(
                TaxCode: OptionalText(t, "codigo") ?? string.Empty,
                TaxRateCode: OptionalText(t, "codigoPorcentaje") ?? string.Empty,
                Rate: OptionalDecimal(t, "tarifa") ?? 0m,
                TaxableBase: OptionalDecimal(t, "baseImponible") ?? 0m,
                TaxAmount: OptionalDecimal(t, "valor") ?? 0m
            ))
            .ToList()
        ?? [];

    // PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 — reutiliza PurchaseXmlAdditionalFieldReader (mismo
    // lector que usa PurchaseXmlDraftParser para persistir el snapshot documental de línea) en vez
    // de mantener una segunda copia de esta lógica solo para la vista.
    private static IReadOnlyList<PurchaseReceptionXmlViewAdditionalDetail> ParseAdditionalDetails(
        XElement? detallesAdicionales
    ) =>
        PurchaseXmlAdditionalFieldReader
            .Read(detallesAdicionales)
            .Select(f => new PurchaseReceptionXmlViewAdditionalDetail(f.Name, f.Value))
            .ToList();

    private static decimal SumIce(IReadOnlyList<PurchaseReceptionXmlViewExtraTax> taxSummaries) =>
        taxSummaries.Where(t => t.TaxCode == SriIceTaxCode).Sum(t => t.TaxAmount);

    private static string? OptionalText(XElement? parent, string name) =>
        parent?.Element(name)?.Value is { Length: > 0 } value ? value : null;

    private static decimal? OptionalDecimal(XElement? parent, string name) =>
        OptionalText(parent, name) is { } text
            ? decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture)
            : null;

    private static DateOnly? OptionalDate(XElement? parent, string name) =>
        OptionalText(parent, name) is { } text
            ? DateOnly.ParseExact(text, "dd/MM/yyyy", CultureInfo.InvariantCulture)
            : null;
}
