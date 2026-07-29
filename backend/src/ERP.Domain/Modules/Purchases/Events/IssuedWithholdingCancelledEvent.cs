using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>Se levanta cuando <c>IssuedWithholding.Cancel()</c> anula una retención emitida.</summary>
public sealed class IssuedWithholdingCancelledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid WithholdingId { get; }
    public Guid PurchaseInvoiceId { get; }
    public Guid SupplierId { get; }
    public string WithholdingNumber { get; }
    public decimal TotalRetained { get; }
    public string CancelReason { get; }

    public IssuedWithholdingCancelledEvent(
        Guid tenantId,
        Guid withholdingId,
        Guid purchaseInvoiceId,
        Guid supplierId,
        string withholdingNumber,
        decimal totalRetained,
        string cancelReason
    )
    {
        TenantId = tenantId;
        WithholdingId = withholdingId;
        PurchaseInvoiceId = purchaseInvoiceId;
        SupplierId = supplierId;
        WithholdingNumber = withholdingNumber;
        TotalRetained = totalRetained;
        CancelReason = cancelReason;
    }

    Guid IAuditEvent.EntityId => WithholdingId;
    string IAuditEvent.Action => "Cancelled";
    string? IAuditEvent.Reason => CancelReason;
}
