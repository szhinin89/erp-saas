using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// FLOW-READY-02C-R1.2 — línea de descuento/crédito de una <see cref="PurchaseCreditNote"/> tipo
/// <c>Discount</c>, aplicada contra un grupo de impuesto real de la compra original
/// (<see cref="PurchaseInvoiceTaxSummary"/> vía <see cref="SourcePurchaseInvoiceTaxSummaryId"/>).
/// VatCode/VatRate/VatName/IceCode/IceRate/IceName son siempre heredados del resumen fiscal de
/// origen — nunca provistos por el cliente ni recalculados desde catálogos vivos.
/// <see cref="IceAmount"/>/<see cref="VatAmount"/> se calculan con <see cref="SriTaxCalculator"/>
/// sobre <see cref="TaxableBase"/> (la base de descuento aplicada, no la base total de la compra).
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

    // ── Identidad de impuesto — heredada del resumen de origen, nunca del cliente ────
    public string VatCode { get; private set; } = null!;
    public decimal VatRate { get; private set; }
    public string? VatName { get; private set; }

    public string? IceCode { get; private set; }
    public decimal IceRate { get; private set; }
    public string? IceName { get; private set; }

    // ── Montos ────────────────────────────────────────────────────────────
    /// <summary>Base de descuento/crédito aplicada por esta NC — nunca la base total de la compra.</summary>
    public decimal TaxableBase { get; private set; }
    public decimal IceAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal TotalAmount { get; private set; }

    public DateTime CreatedAt { get; private set; }

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
        string vatCode,
        decimal vatRate,
        string? vatName,
        string? iceCode,
        decimal iceRate,
        string? iceName,
        decimal taxableBase,
        decimal iceAmount,
        decimal vatAmount
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
        if (string.IsNullOrWhiteSpace(vatCode))
            throw new ArgumentException("El código IVA es obligatorio.", nameof(vatCode));
        if (taxableBase <= 0)
            throw new ArgumentException(
                "La base de descuento debe ser mayor a cero.",
                nameof(taxableBase)
            );

        return new PurchaseCreditNoteTaxSummary
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            PurchaseCreditNoteId = purchaseCreditNoteId,
            PurchaseInvoiceId = purchaseInvoiceId,
            SourcePurchaseInvoiceTaxSummaryId = sourcePurchaseInvoiceTaxSummaryId,
            VatCode = vatCode.Trim(),
            VatRate = vatRate,
            VatName = string.IsNullOrWhiteSpace(vatName) ? null : vatName.Trim(),
            IceCode = OptionalCode.Normalize(iceCode),
            IceRate = iceRate,
            IceName = string.IsNullOrWhiteSpace(iceName) ? null : iceName.Trim(),
            TaxableBase = taxableBase,
            IceAmount = iceAmount,
            VatAmount = vatAmount,
            TotalAmount = taxableBase + iceAmount + vatAmount,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
