using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Employees.Enums;

namespace ERP.Domain.Modules.Employees.Events;

/// <summary>
/// Cambio auditable de un registro de catálogo laboral (cargo, profesión o especialidad):
/// <c>Created</c>, <c>Renamed</c>, <c>Deactivated</c> o <c>Activated</c>.
/// </summary>
public sealed class EmployeeCatalogChangedEvent : BaseDomainEvent, IAuditEvent
{
    public const string CreatedAction = "Created";
    public const string RenamedAction = "Renamed";
    public const string DeactivatedAction = "Deactivated";
    public const string ActivatedAction = "Activated";

    public Guid CompanyId { get; }
    public EmployeeCatalogType CatalogType { get; }
    public Guid CatalogEntryId { get; }
    public string Action { get; }
    public string? OldName { get; }
    public string NewName { get; }
    public bool IsActive { get; }

    public EmployeeCatalogChangedEvent(
        Guid tenantId,
        Guid companyId,
        EmployeeCatalogType catalogType,
        Guid catalogEntryId,
        string action,
        string? oldName,
        string newName,
        bool isActive
    )
    {
        TenantId = tenantId;
        CompanyId = companyId;
        CatalogType = catalogType;
        CatalogEntryId = catalogEntryId;
        Action = action;
        OldName = oldName;
        NewName = newName;
        IsActive = isActive;
    }

    Guid IAuditEvent.EntityId => CatalogEntryId;
    string IAuditEvent.Action => Action;
    string? IAuditEvent.Reason => null;
}
