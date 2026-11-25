using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Enums;

namespace ERP.Domain.Modules.Employees.Services;

/// <summary>Crea el registro del catálogo que corresponde al tipo (una tabla por catálogo, DEC-E5).</summary>
public static class EmployeeCatalogFactory
{
    public static EmployeeCatalogEntry Create(
        EmployeeCatalogType type,
        Guid tenantId,
        Guid companyId,
        string name,
        Guid createdBy
    ) =>
        type switch
        {
            EmployeeCatalogType.JobPosition => JobPosition.Create(tenantId, companyId, name, createdBy),
            EmployeeCatalogType.Profession => Profession.Create(tenantId, companyId, name, createdBy),
            EmployeeCatalogType.Specialty => Specialty.Create(tenantId, companyId, name, createdBy),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Catálogo desconocido."),
        };
}
