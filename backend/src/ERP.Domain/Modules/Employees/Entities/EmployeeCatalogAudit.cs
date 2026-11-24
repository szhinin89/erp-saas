using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Employees.Enums;

namespace ERP.Domain.Modules.Employees.Entities;

/// <summary>
/// Auditoría común de los tres catálogos laborales (EMP-T-07). Append-only; el tipo de catálogo
/// distingue el origen.
/// </summary>
public sealed class EmployeeCatalogAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public EmployeeCatalogType CatalogType { get; private set; }
    public string? OldName { get; private set; }
    public string NewName { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private EmployeeCatalogAudit() { }

    public static EmployeeCatalogAudit Create(
        AuditActor actor,
        Guid companyId,
        EmployeeCatalogType catalogType,
        Guid catalogEntryId,
        string action,
        string? oldName,
        string newName,
        bool isActive
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new EmployeeCatalogAudit
        {
            CompanyId = companyId,
            CatalogType = catalogType,
            OldName = oldName,
            NewName = newName,
            IsActive = isActive,
        };
        audit.SetCommon(actor, catalogEntryId, action, null);
        return audit;
    }
}
