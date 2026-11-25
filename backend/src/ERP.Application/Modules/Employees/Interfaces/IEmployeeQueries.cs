using ERP.Application.Modules.Employees.DTOs;

namespace ERP.Application.Modules.Employees.Interfaces;

/// <summary>
/// Lecturas de Empleados con proyección del BP (D-5): sin seguimiento, con tenant y empresa
/// explícitos, sin N+1.
/// </summary>
/// <summary>
/// Proyección del usuario vinculado según IAM-V1-F1 (la implementa Application sobre la regla única de IAM).
/// </summary>
public interface IEmployeeIdentityProjector
{
    Task<EmployeeIdentityUserDto?> ProjectAsync(Guid companyId, Guid identityUserId, CancellationToken ct = default);
}

public interface IEmployeeQueries
{
    Task<EmployeeDto?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        CancellationToken ct = default
    );

    /// <summary>Historial de asignaciones de la ficha; null si la ficha no existe o es de otro ámbito.</summary>
    Task<IReadOnlyList<EmployeeBranchAssignmentDto>?> ListAssignmentsAsync(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        CancellationToken ct = default
    );

    Task<EmployeeBranchAssignmentDto?> GetAssignmentAsync(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        Guid assignmentId,
        CancellationToken ct = default
    );

    Task<(IReadOnlyList<EmployeeListItemDto> Items, int Total)> ListAsync(
        Guid tenantId,
        Guid companyId,
        EmployeeListFilter filter,
        int skip,
        int take,
        CancellationToken ct = default
    );
}
