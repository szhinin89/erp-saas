using ERP.Domain.Common;

namespace ERP.Domain.Modules.Employees.Entities;

/// <summary>
/// Vínculo empleado ↔ especialidad (DEC-E5). Retirar y reasignar alterna <see cref="IsActive"/>
/// en la misma fila (EMP-BR-06; UX-SPL-01 impide filas duplicadas). Las operaciones pertenecen
/// a EMP-E4.
/// </summary>
public sealed class EmployeeSpecialty : AuditableEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid SpecialtyId { get; private set; }
    public bool IsActive { get; private set; }

    private EmployeeSpecialty() { }

    public static EmployeeSpecialty Create(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        Guid specialtyId,
        Guid createdBy
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId requerido.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));
        if (employeeId == Guid.Empty)
            throw new ArgumentException("employeeId requerido.", nameof(employeeId));
        if (specialtyId == Guid.Empty)
            throw new ArgumentException("specialtyId requerido.", nameof(specialtyId));

        var link = new EmployeeSpecialty
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            EmployeeId = employeeId,
            SpecialtyId = specialtyId,
            IsActive = true,
        };
        link.SetCreated(createdBy);
        return link;
    }
}
