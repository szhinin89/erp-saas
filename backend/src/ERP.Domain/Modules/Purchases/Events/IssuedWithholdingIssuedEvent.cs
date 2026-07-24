using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>Se levanta cuando <c>IssuedWithholding.Issue()</c> emite una retención en la fuente.</summary>
public sealed class IssuedWithholdingIssuedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid WithholdingId { get; }
    public Guid PurchaseInvoiceId { get; }
    public Guid SupplierId { get; }
    public string WithholdingNumber { get; }
    public decimal TotalRetained { get; }

    public IssuedWithholdingIssuedEvent(
        Guid tenantId, Guid withholdingId, Guid purchaseInvoiceId, Guid supplierId,
        string withholdingNumber, decimal totalRetained)
    {
        TenantId = tenantId;
        WithholdingId = withholdingId;
        PurchaseInvoiceId = purchaseInvoiceId;
        SupplierId = supplierId;
        WithholdingNumber = withholdingNumber;
        TotalRetained = totalRetained;
    }

    Guid IAuditEvent.EntityId => WithholdingId;
    string IAuditEvent.Action => "Issued";
    string? IAuditEvent.Reason => null;
}
