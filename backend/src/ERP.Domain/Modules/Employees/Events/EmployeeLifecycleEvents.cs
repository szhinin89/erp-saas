using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Employees.Events;

/// <summary>Baja laboral (EMP-BR-08): cierre del período abierto con su motivo opcional.</summary>
public sealed class EmployeeTerminatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid CompanyId { get; }
    public Guid EmployeeId { get; }
    public Guid BusinessPartnerId { get; }
    public DateOnly EndDate { get; }
    public string? Reason { get; }

    public EmployeeTerminatedEvent(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        Guid businessPartnerId,
        DateOnly endDate,
        string? reason
    )
    {
        TenantId = tenantId;
        CompanyId = companyId;
        EmployeeId = employeeId;
        BusinessPartnerId = businessPartnerId;
        EndDate = endDate;
        Reason = reason;
    }

    Guid IAuditEvent.EntityId => EmployeeId;
    string IAuditEvent.Action => "Terminated";
    string? IAuditEvent.Reason => Reason;
}

/// <summary>Reincorporación (EMP-BR-09): apertura de un nuevo período laboral.</summary>
public sealed class EmployeeRehiredEvent : BaseDomainEvent, IAuditEvent
{
    public Guid CompanyId { get; }
    public Guid EmployeeId { get; }
    public Guid BusinessPartnerId { get; }
    public DateOnly StartDate { get; }

    public EmployeeRehiredEvent(
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
    string IAuditEvent.Action => "Rehired";
    string? IAuditEvent.Reason => null;
}

/// <summary>Asignación a sucursal creada (<c>BranchAssigned</c>) o cerrada (<c>BranchAssignmentClosed</c>).</summary>
public sealed class EmployeeBranchAssignmentChangedEvent : BaseDomainEvent, IAuditEvent
{
    public const string AssignedAction = "BranchAssigned";
    public const string ClosedAction = "BranchAssignmentClosed";

    public Guid CompanyId { get; }
    public Guid EmployeeId { get; }
    public Guid BusinessPartnerId { get; }
    public string Action { get; }
    public Guid BranchId { get; }
    public DateOnly StartDate { get; }
    public DateOnly? OldEndDate { get; }
    public DateOnly? NewEndDate { get; }

    public EmployeeBranchAssignmentChangedEvent(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        Guid businessPartnerId,
        string action,
        Guid branchId,
        DateOnly startDate,
        DateOnly? oldEndDate,
        DateOnly? newEndDate
    )
    {
        TenantId = tenantId;
        CompanyId = companyId;
        EmployeeId = employeeId;
        BusinessPartnerId = businessPartnerId;
        Action = action;
        BranchId = branchId;
        StartDate = startDate;
        OldEndDate = oldEndDate;
        NewEndDate = newEndDate;
    }

    Guid IAuditEvent.EntityId => EmployeeId;
    string IAuditEvent.Action => Action;
    string? IAuditEvent.Reason => null;
}
