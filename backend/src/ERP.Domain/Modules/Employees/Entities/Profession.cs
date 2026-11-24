using ERP.Domain.Modules.Employees.Enums;

namespace ERP.Domain.Modules.Employees.Entities;

/// <summary>Profesión por empresa (catálogo independiente, DEC-E5).</summary>
public sealed class Profession : EmployeeCatalogEntry
{
    public override EmployeeCatalogType CatalogType => EmployeeCatalogType.Profession;

    private Profession() { }

    public static Profession Create(Guid tenantId, Guid companyId, string name, Guid createdBy)
    {
        var entry = new Profession();
        entry.Initialize(tenantId, companyId, name, createdBy);
        return entry;
    }
}
