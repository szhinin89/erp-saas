using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Employees.Events;

/// <summary>
/// Cambio auditable de un campo de la ficha (cargo, profesión o especialidad). Los valores son
/// identificadores o estados, nunca datos personales (EMP-BR-20).
/// </summary>
public sealed class EmployeeFieldChangedEvent : BaseDomainEvent, IAuditEvent
{
    public const string JobPositionField = "JobPosition";
    public const string ProfessionField = "Profession";
    public const string SpecialtyField = "Specialty";
    public const string UpdatedAction = "Updated";

    public Guid CompanyId { get; }
    public Guid EmployeeId { get; }
    public Guid BusinessPartnerId { get; }
    public string ChangedField { get; }
    public string? OldValue { get; }
    public string? NewValue { get; }

    public EmployeeFieldChangedEvent(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        Guid businessPartnerId,
        string changedField,
        string? oldValue,
        string? newValue
    )
    {
        TenantId = tenantId;
        CompanyId = companyId;
        EmployeeId = employeeId;
        BusinessPartnerId = businessPartnerId;
        ChangedField = changedField;
        OldValue = oldValue;
        NewValue = newValue;
    }

    Guid IAuditEvent.EntityId => EmployeeId;
    string IAuditEvent.Action => UpdatedAction;
    string? IAuditEvent.Reason => null;
}
