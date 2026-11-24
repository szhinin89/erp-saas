using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Employees.Events;

/// <summary>Se levanta cuando <c>Employee.Create()</c> crea la ficha y abre su primer período.</summary>
public sealed class EmployeeCreatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid CompanyId { get; }
    public Guid EmployeeId { get; }
    public Guid BusinessPartnerId { get; }
    public DateOnly StartDate { get; }

    public EmployeeCreatedEvent(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        Guid businessPartnerId,
        DateOnly startDate
    )
    {
        TenantId = tenantId;
        CompanyId = companyId;
        EmployeeId = employeeId;
        BusinessPartnerId = businessPartnerId;
        StartDate = startDate;
    }

    Guid IAuditEvent.EntityId => EmployeeId;
    string IAuditEvent.Action => "Created";
    string? IAuditEvent.Reason => null;
}
