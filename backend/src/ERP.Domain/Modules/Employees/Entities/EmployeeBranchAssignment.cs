using ERP.Domain.Common;

namespace ERP.Domain.Modules.Employees.Entities;

/// <summary>
/// Sucursal laboral de un empleado con historial (DEC-E6). No es <c>CompanyUserBranch</c>: no
/// concede acceso ni permisos (INV-11). Sin sucursal principal (P-04). Modelo de datos de EMP-E2;
/// las operaciones de asignación pertenecen a EMP-E5.
/// </summary>
public sealed class EmployeeBranchAssignment : AuditableEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid BranchId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }

    private EmployeeBranchAssignment() { }

    public static EmployeeBranchAssignment Create(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        Guid branchId,
        DateOnly startDate,
        DateOnly? endDate,
        Guid createdBy
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId requerido.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));
        if (employeeId == Guid.Empty)
            throw new ArgumentException("employeeId requerido.", nameof(employeeId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (startDate == default)
            throw new ArgumentException("La fecha de inicio es obligatoria.", nameof(startDate));
        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException(
                "La fecha fin no puede ser anterior a la fecha inicio.",
                nameof(endDate)
            );

        var assignment = new EmployeeBranchAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            EmployeeId = employeeId,
            BranchId = branchId,
            StartDate = startDate,
            EndDate = endDate,
        };
        assignment.SetCreated(createdBy);
        return assignment;
    }
}
