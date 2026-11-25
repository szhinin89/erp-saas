using ERP.Application.Modules.Employees.DTOs;

namespace ERP.Application.Modules.Employees.Interfaces;

/// <summary>
/// Lecturas de Empleados con proyección del BP (D-5): sin seguimiento, con tenant y empresa
/// explícitos, sin N+1.
/// </summary>
public interface IEmployeeQueries
{
    Task<EmployeeDto?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
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
