using ERP.Domain.Modules.Employees.Entities;

namespace ERP.Domain.Modules.Employees.Interfaces;

/// <summary>
/// Persistencia de la ficha de empleado (EMP-E4). Todas las consultas reciben tenant y empresa
/// explícitos y los aplican como predicado propio, sin depender solo del filtro global (observación B).
/// </summary>
public interface IEmployeeRepository
{
    /// <summary>
    /// Lock transaccional por ficha <c>("Employees", employeeId)</c>. Exige transacción activa en
    /// READ COMMITTED. Orden global: Employees → BpEmployment.
    /// </summary>
    Task AcquireEmployeeLockAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>Ficha de la empresa operativa (con seguimiento) o null si no existe o es de otro ámbito.</summary>
    Task<Employee?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default);

    /// <summary>¿Existe ya una ficha del BP en la empresa? Lectura fresca, sin seguimiento (UX-EMP-01).</summary>
    Task<bool> ExistsForBusinessPartnerAsync(
        Guid tenantId,
        Guid companyId,
        Guid businessPartnerId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Ficha con todos sus períodos (con seguimiento) para la baja, la reincorporación y las asignaciones;
    /// null si no existe o es de otro ámbito.
    /// </summary>
    Task<Employee?> GetWithPeriodsAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default);

    /// <summary>Token de concurrencia (<c>xmin</c>) vigente de una ficha leída con seguimiento.</summary>
    uint GetRowVersion(Employee employee);

    /// <summary>Asignaciones a sucursal de la ficha (abiertas y cerradas), con seguimiento.</summary>
    Task<IReadOnlyList<EmployeeBranchAssignment>> GetAssignmentsAsync(
        Guid tenantId, Guid companyId, Guid employeeId, CancellationToken ct = default);

    /// <summary>Asignación de esa ficha, tenant y empresa (con seguimiento); null si no corresponde.</summary>
    Task<EmployeeBranchAssignment?> GetAssignmentAsync(
        Guid tenantId, Guid companyId, Guid employeeId, Guid assignmentId, CancellationToken ct = default);

    Task AddAssignmentAsync(EmployeeBranchAssignment assignment, CancellationToken ct = default);

    /// <summary>Registra como nuevo el período abierto por una reincorporación.</summary>
    Task AddPeriodAsync(EmployeeEmploymentPeriod period, CancellationToken ct = default);

    /// <summary>Token de concurrencia (<c>xmin</c>) vigente de una asignación leída con seguimiento.</summary>
    uint GetAssignmentRowVersion(EmployeeBranchAssignment assignment);

    /// <summary>Fija el token recibido del cliente como valor original de la asignación.</summary>
    void SetExpectedAssignmentRowVersion(EmployeeBranchAssignment assignment, uint rowVersion);

    Task AddAsync(Employee employee, CancellationToken ct = default);

    /// <summary>Fija el token de concurrencia (<c>xmin</c>) recibido del cliente como valor original.</summary>
    void SetExpectedRowVersion(Employee employee, uint rowVersion);

    /// <summary>Vínculos de especialidad de la ficha (activos e inactivos), con seguimiento.</summary>
    Task<IReadOnlyList<EmployeeSpecialty>> GetSpecialtyLinksAsync(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        CancellationToken ct = default
    );

    Task AddSpecialtyLinkAsync(EmployeeSpecialty link, CancellationToken ct = default);

    /// <summary>
    /// Persiste los cambios. <c>false</c> si la versión de la ficha está obsoleta (concurrencia
    /// optimista); en ese caso nada se confirma y el llamador revierte.
    /// </summary>
    Task<bool> SaveChangesAsync(CancellationToken ct = default);
}
