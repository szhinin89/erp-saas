using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// Nota de Crédito del proveedor vinculada a una <see cref="Entities.PurchaseReturn"/> — diseño
/// P0-02 §9.1/§18, Fase 9. Puramente documental: <see cref="Entities.PurchaseReturn.LinkSupplierCreditNote"/>
/// no tiene ningún efecto sobre inventario/CxP/crédito/contabilidad (§18.5) — este evento existe
/// únicamente para que <c>PurchaseReturnAuditHandler</c> (ADR-022, Entity Audit) registre el
/// vínculo, nunca para disparar un <c>PostingFact</c> (§19.5). Ningún traductor de Posting debe
/// implementarse para este evento.
/// </summary>
public sealed class PurchaseReturnSupplierCreditNoteLinkedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid PurchaseReturnId { get; }
    public Guid PurchaseInvoiceId { get; }
    public Guid SupplierId { get; }
    public Guid BranchId { get; }
    public Guid CompanyId { get; }
    public string ReturnNumber { get; }
    public decimal GrandTotal { get; }
    public Guid SupplierCreditNoteDocumentId { get; }
    public Guid UserId { get; }

    Guid IAuditEvent.EntityId => PurchaseReturnId;
    string IAuditEvent.Action => "SupplierCreditNoteLinked";
    string? IAuditEvent.Reason => null;

    public PurchaseReturnSupplierCreditNoteLinkedEvent(
        Guid purchaseReturnId,
        Guid purchaseInvoiceId,
        Guid supplierId,
        Guid branchId,
        Guid tenantId,
        Guid companyId,
        string returnNumber,
        decimal grandTotal,
        Guid supplierCreditNoteDocumentId,
        Guid userId
    )
    {
        PurchaseReturnId = purchaseReturnId;
        PurchaseInvoiceId = purchaseInvoiceId;
        SupplierId = supplierId;
        BranchId = branchId;
        TenantId = tenantId;
        CompanyId = companyId;
        ReturnNumber = returnNumber;
        GrandTotal = grandTotal;
        SupplierCreditNoteDocumentId = supplierCreditNoteDocumentId;
        UserId = userId;
    }
}
