using ERP.Domain.Modules.Employees.Enums;

namespace ERP.Domain.Modules.Employees.Entities;

/// <summary>Especialidad por empresa (catálogo independiente, DEC-E5).</summary>
public sealed class Specialty : EmployeeCatalogEntry
{
    public override EmployeeCatalogType CatalogType => EmployeeCatalogType.Specialty;

    private Specialty() { }

    public static Specialty Create(Guid tenantId, Guid companyId, string name, Guid createdBy)
    {
        var entry = new Specialty();
        entry.Initialize(tenantId, companyId, name, createdBy);
        return entry;
    }
}
