using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// Nota de crédito de compra (descuento/promoción) autorizada — diseño FLOW-READY-02C §4.3 (ajuste
/// obligatorio #2). Punto de extensión inerte: deliberadamente NO implementa <c>IAuditEvent</c> y no
/// tiene ningún handler/traductor contable registrado en esta fase — publicarlo no genera
/// <c>PostingFact</c> ni entrada de auditoría. Una fase futura que decida darle efecto contable debe
/// hacerlo explícitamente (su propio handler + su propia confirmación/ADR), sin modificar este evento.
/// </summary>
public sealed class PurchaseCreditNoteAuthorizedEvent : BaseDomainEvent
{
    public Guid PurchaseCreditNoteId { get; }
    public Guid PurchaseInvoiceId { get; }
    public Guid SupplierId { get; }
    public Guid BranchId { get; }
    public Guid CompanyId { get; }
    public string CreditNoteNumber { get; }
    public Guid UserId { get; }

    public decimal Subtotal { get; }
    public decimal VatAmount { get; }
    public decimal TotalAmount { get; }
    public decimal AppliedToPayableAmount { get; }

    public PurchaseCreditNoteAuthorizedEvent(
        Guid purchaseCreditNoteId,
        Guid purchaseInvoiceId,
        Guid supplierId,
        Guid branchId,
        Guid tenantId,
        Guid companyId,
        string creditNoteNumber,
        Guid userId,
        decimal subtotal,
        decimal vatAmount,
        decimal totalAmount,
        decimal appliedToPayableAmount
    )
    {
        PurchaseCreditNoteId = purchaseCreditNoteId;
        PurchaseInvoiceId = purchaseInvoiceId;
        SupplierId = supplierId;
        BranchId = branchId;
        TenantId = tenantId;
        CompanyId = companyId;
        CreditNoteNumber = creditNoteNumber;
        UserId = userId;
        Subtotal = subtotal;
        VatAmount = vatAmount;
        TotalAmount = totalAmount;
        AppliedToPayableAmount = appliedToPayableAmount;
    }
}
