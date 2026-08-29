using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// Nota de crédito de compra (descuento/promoción) autorizada — diseño FLOW-READY-02C §4.3 (ajuste
/// obligatorio #2). Deliberadamente sin <c>IAuditEvent</c> (decisión original sin cambios). Desde
/// ACCOUNTING-CREDIT-NOTES-POSTING-08 SÍ tiene efecto contable —
/// <c>PurchaseCreditNoteAuthorizedPostingTranslator</c> es esa "fase futura que decida darle
/// efecto contable" que este comentario ya anticipaba: su propio handler nuevo, sin modificar este
/// evento (más allá de <see cref="IceAmount"/>, ver siguiente nota).
/// </summary>
/// <remarks>
/// ACCOUNTING-PURCHASE-CREDIT-NOTE-ICE-08B: <see cref="IceAmount"/> se agregó al FINAL de la lista
/// posicional del constructor (aditivo puro, mismo criterio ya usado en <c>PostingFact</c> — nunca
/// rompe el único call site existente, <c>PurchaseCreditNote.Authorize()</c>) porque el valor ya
/// existía en la entidad (<c>PurchaseCreditNote.IceAmount</c>, calculado por
/// <c>RecalculateTotals()</c>) pero nunca se propagaba al evento — una NC de compra con ICE nunca
/// podía contabilizar ese componente porque el traductor ni siquiera tenía el dato.
/// </remarks>
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

    /// <summary>ACCOUNTING-PURCHASE-CREDIT-NOTE-ICE-08B — ya incluido en <see cref="TotalAmount"/>, expuesto aparte para que Accounting pueda contabilizarlo como línea propia.</summary>
    public decimal IceAmount { get; }

    /// <summary>TAX-LINE-SSOT-ICE-IRBPNR-01 Fase 5E — ya incluido en <see cref="TotalAmount"/> (<c>PurchaseCreditNote.IrbpnrAmount</c>), expuesto aparte para que Accounting pueda contabilizarlo como línea propia (mismo criterio que <see cref="IceAmount"/>).</summary>
    public decimal IrbpnrAmount { get; }

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
        decimal appliedToPayableAmount,
        decimal iceAmount = 0m,
        decimal irbpnrAmount = 0m
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
        IceAmount = iceAmount;
        IrbpnrAmount = irbpnrAmount;
    }
}
