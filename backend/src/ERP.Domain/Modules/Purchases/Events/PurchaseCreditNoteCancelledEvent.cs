using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// Nota de crédito de compra (descuento/promoción) cancelada — mismo criterio de diseño que
/// <see cref="PurchaseCreditNoteAuthorizedEvent"/> (FLOW-READY-02C §4.3): sin <c>IAuditEvent</c>.
/// Desde ACCOUNTING-CREDIT-NOTES-POSTING-08, <c>PurchaseCreditNoteCancelledPostingTranslator</c>
/// reversa el asiento original si <see cref="AppliedToPayableAmount"/> no es null (la NC llegó a
/// autorizarse y contabilizarse); si es null (cancelada desde Draft), no hay nada que reversar.
/// </summary>
public sealed class PurchaseCreditNoteCancelledEvent : BaseDomainEvent
{
    public Guid PurchaseCreditNoteId { get; }
    public Guid PurchaseInvoiceId { get; }
    public Guid SupplierId { get; }
    public Guid BranchId { get; }
    public Guid CompanyId { get; }
    public string? CreditNoteNumber { get; }
    public string Reason { get; }
    public Guid UserId { get; }

    /// <summary>Solo tiene valor si se cancela desde <c>Authorized</c> — null si se cancela desde <c>Draft</c>.</summary>
    public decimal? AppliedToPayableAmount { get; }

    public PurchaseCreditNoteCancelledEvent(
        Guid purchaseCreditNoteId,
        Guid purchaseInvoiceId,
        Guid supplierId,
        Guid branchId,
        Guid tenantId,
        Guid companyId,
        string? creditNoteNumber,
        string reason,
        Guid userId,
        decimal? appliedToPayableAmount
    )
    {
        PurchaseCreditNoteId = purchaseCreditNoteId;
        PurchaseInvoiceId = purchaseInvoiceId;
        SupplierId = supplierId;
        BranchId = branchId;
        TenantId = tenantId;
        CompanyId = companyId;
        CreditNoteNumber = creditNoteNumber;
        Reason = reason;
        UserId = userId;
        AppliedToPayableAmount = appliedToPayableAmount;
    }
}
