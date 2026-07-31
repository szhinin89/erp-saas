using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Events;

/// <summary>
/// Se levanta cuando <c>SalesReturn.CreateDraft(...)</c> crea un nuevo borrador de devolución.
/// Sin totales — al momento de crear el Draft todavía no tiene líneas congeladas (se agregan
/// después vía <c>AddLine</c>), mismo criterio que <c>ItemCreatedEvent</c> no snapshotea precio.
/// </summary>
public sealed class SalesReturnDraftCreatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid SalesReturnId { get; }
    public Guid SalesInvoiceId { get; }
    public Guid CustomerId { get; }
    public string ReturnNumber { get; }
    public string Reason { get; }
    public Guid CompanyId { get; }
    public Guid UserId { get; }

    public SalesReturnDraftCreatedEvent(
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
    string IAuditEvent.Action => "Created";
    string? IAuditEvent.Reason => Reason;
}
