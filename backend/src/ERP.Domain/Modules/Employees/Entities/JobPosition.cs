using ERP.Domain.Modules.Employees.Enums;

namespace ERP.Domain.Modules.Employees.Entities;

/// <summary>Cargo laboral por empresa (catálogo independiente, DEC-E5).</summary>
public sealed class JobPosition : EmployeeCatalogEntry
{
    public override EmployeeCatalogType CatalogType => EmployeeCatalogType.JobPosition;

    private JobPosition() { }

    public static JobPosition Create(Guid tenantId, Guid companyId, string name, Guid createdBy)
    {
        var entry = new JobPosition();
        entry.Initialize(tenantId, companyId, name, createdBy);
        return entry;
    }
}
