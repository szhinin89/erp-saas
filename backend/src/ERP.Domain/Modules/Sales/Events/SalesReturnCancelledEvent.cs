using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Events;

/// <summary>
/// Se levanta cuando <c>SalesReturn.Cancel()</c> anula un borrador de devolución. Solo es legal
/// desde <see cref="Enums.SalesReturnStatus.Draft"/>, por lo que nunca hay totales congelados que
/// snapshotear (mismo motivo por el que <see cref="SalesReturnDraftCreatedEvent"/> tampoco los tiene).
/// </summary>
public sealed class SalesReturnCancelledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid SalesReturnId { get; }
    public Guid SalesInvoiceId { get; }
    public Guid CustomerId { get; }
    public string ReturnNumber { get; }
    public string Reason { get; }
    public Guid CompanyId { get; }
    public Guid UserId { get; }

    public SalesReturnCancelledEvent(
        Guid salesReturnId,
        Guid salesInvoiceId,
        Guid customerId,
        string returnNumber,
        string reason,
        Guid tenantId,
        Guid companyId,
        Guid userId
    )
    {
        SalesReturnId = salesReturnId;
        SalesInvoiceId = salesInvoiceId;
        CustomerId = customerId;
        ReturnNumber = returnNumber;
        Reason = reason;
        TenantId = tenantId;
        CompanyId = companyId;
        UserId = userId;
    }

    Guid IAuditEvent.EntityId => SalesReturnId;
    string IAuditEvent.Action => "Cancelled";
    string? IAuditEvent.Reason => Reason;
}
