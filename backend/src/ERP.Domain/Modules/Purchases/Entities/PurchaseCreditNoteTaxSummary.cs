using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// FLOW-READY-02C-R1.2 — línea de descuento/crédito de una <see cref="PurchaseCreditNote"/> tipo
/// <c>Discount</c>, aplicada contra un grupo de impuesto real de la compra original
/// (<see cref="PurchaseInvoiceTaxSummary"/> vía <see cref="SourcePurchaseInvoiceTaxSummaryId"/>).
///
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-2 — corrección post-revisión): la identidad
/// y el monto de cada impuesto (IVA/ICE/IRBPNR/futuro) viven exclusivamente en
/// <see cref="Taxes"/> (<see cref="PurchaseCreditNoteTaxSummaryLine"/>, una fila por impuesto real) —
/// nunca como columna fija por impuesto. VatCode/VatRate/.../IrbpnrAmount de abajo son propiedades
/// **derivadas** de esa colección (mismo patrón que <c>PurchaseInvoiceDetail.IrbpnrAmount</c>), no
/// columnas persistidas independientes — se mantienen únicamente para no romper a los consumidores
/// existentes (<c>CreditNoteMap.ToDto</c>) que ya leen estos nombres. Ningún código nuevo debe
/// agregar más propiedades derivadas por impuesto: para IVA/ICE/IRBPNR el patrón de acceso correcto
/// es <see cref="Taxes"/>.
///
/// Nunca editable de forma independiente — sin factory pública, sin métodos de mutación; la única
/// vía es <c>PurchaseCreditNote.CreateDraft</c>/<c>UpdateDraft</c>.
/// </summary>
public sealed class PurchaseCreditNoteTaxSummary : ICompanyOperationalEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }

    /// <summary>Snapshot de <see cref="PurchaseCreditNote.BranchId"/> — Branch Ownership Rule, nunca mutable.</summary>
    public Guid BranchId { get; private set; }

    public Guid PurchaseCreditNoteId { get; private set; }
    public Guid PurchaseInvoiceId { get; private set; }

    /// <summary>Resumen fiscal de la compra original del cual esta línea deriva su identidad de impuesto.</summary>
    public Guid SourcePurchaseInvoiceTaxSummaryId { get; private set; }

    /// <summary>Base de descuento/crédito aplicada por esta NC — nunca la base total de la compra.</summary>
    public decimal TaxableBase { get; private set; }
    public decimal TotalAmount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    // ── Impuestos por línea (ADR-032 §3.3) — fuente de verdad ───────────────────────────────────
    private readonly List<PurchaseCreditNoteTaxSummaryLine> _taxes = new();
    public IReadOnlyList<PurchaseCreditNoteTaxSummaryLine> Taxes => _taxes.AsReadOnly();

    private const string VatSriTaxCode = SriTaxCategoryCodes.Vat;
    private const string IceSriTaxCode = SriTaxCategoryCodes.Ice;
    private const string IrbpnrSriTaxCode = SriTaxCategoryCodes.Irbpnr;

    // ── Propiedades derivadas — legacy compatibility mirror, no fuente de verdad ────────────────
    public string VatCode => _taxes.First(t => t.TaxCode == VatSriTaxCode).TaxRateCode;
    public decimal VatRate => _taxes.First(t => t.TaxCode == VatSriTaxCode).Rate ?? 0m;
    public string? VatName => _taxes.First(t => t.TaxCode == VatSriTaxCode).TaxName;
    public decimal VatAmount => _taxes.Where(t => t.TaxCode == VatSriTaxCode).Sum(t => t.TaxAmount);

    public string? IceCode => _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode)?.TaxRateCode;
    public decimal IceRate => _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode)?.Rate ?? 0m;
    public string? IceName => _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode)?.TaxName;
    public decimal IceAmount => _taxes.Where(t => t.TaxCode == IceSriTaxCode).Sum(t => t.TaxAmount);

    /// <summary>IRBPNR nunca se trata como ICE — código, catálogo y resolución siempre separados.</summary>
    public string? IrbpnrCode => _taxes.FirstOrDefault(t => t.TaxCode == IrbpnrSriTaxCode)?.TaxRateCode;
    public decimal IrbpnrRate => _taxes.FirstOrDefault(t => t.TaxCode == IrbpnrSriTaxCode)?.Rate ?? 0m;
    public string? IrbpnrName => _taxes.FirstOrDefault(t => t.TaxCode == IrbpnrSriTaxCode)?.TaxName;
    public decimal IrbpnrAmount => _taxes.Where(t => t.TaxCode == IrbpnrSriTaxCode).Sum(t => t.TaxAmount);

    private PurchaseCreditNoteTaxSummary() { }

    /// <summary>
    /// Factory interno — invocable únicamente desde <see cref="PurchaseCreditNote"/> (mismo
    /// assembly). No hay forma pública de construir ni editar una línea de forma independiente.
    /// </summary>
    internal static PurchaseCreditNoteTaxSummary Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid purchaseCreditNoteId,
        Guid purchaseInvoiceId,
        Guid sourcePurchaseInvoiceTaxSummaryId,
        decimal taxableBase,
        IEnumerable<PurchaseCreditNoteTaxLine> taxes
    )
    {
        if (purchaseCreditNoteId == Guid.Empty)
            throw new ArgumentException(
                "La nota de crédito destino es obligatoria.",
                nameof(purchaseCreditNoteId)
            );
        if (purchaseInvoiceId == Guid.Empty)
            throw new ArgumentException(
                "La factura de compra afectada es obligatoria.",
                nameof(purchaseInvoiceId)
            );
        if (sourcePurchaseInvoiceTaxSummaryId == Guid.Empty)
            throw new ArgumentException(
                "El resumen fiscal de compra de origen es obligatorio.",
                nameof(sourcePurchaseInvoiceTaxSummaryId)
            );
        if (taxableBase <= 0)
            throw new ArgumentException(
                "La base de descuento debe ser mayor a cero.",
                nameof(taxableBase)
            );

        var materializedTaxes = taxes.ToList();
        if (!materializedTaxes.Any(t => t.TaxCode == VatSriTaxCode))
            throw new ArgumentException(
                "El impuesto IVA es obligatorio en el resumen fiscal.",
                nameof(taxes)
            );

        var summary = new PurchaseCreditNoteTaxSummary
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            PurchaseCreditNoteId = purchaseCreditNoteId,
            PurchaseInvoiceId = purchaseInvoiceId,
            SourcePurchaseInvoiceTaxSummaryId = sourcePurchaseInvoiceTaxSummaryId,
            TaxableBase = taxableBase,
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var t in materializedTaxes)
            summary._taxes.Add(
                PurchaseCreditNoteTaxSummaryLine.Create(
                    summary.Id,
                    tenantId,
                    t.TaxCode,
                    t.TaxRateCode,
                    t.TaxName,
                    t.Rate,
                    t.CalculationType,
                    t.TaxAmount
                )
            );

        summary.TotalAmount = taxableBase + materializedTaxes.Sum(t => t.TaxAmount);
        return summary;
    }
}

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-2) — impuesto ya resuelto/prorrateado por
/// <c>PurchaseCreditNote.ReplaceTaxSummaryLines</c>, listo para persistirse como
/// <see cref="PurchaseCreditNoteTaxSummaryLine"/>. Tipo de transporte interno — nunca decide montos.
/// </summary>
public readonly record struct PurchaseCreditNoteTaxLine(
    string TaxCode,
    string TaxRateCode,
    string TaxName,
    decimal? Rate,
    ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType CalculationType,
    decimal TaxAmount
);
