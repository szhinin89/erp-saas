using ERP.Application.Modules.Employees.DTOs;

namespace ERP.Application.Modules.Employees.Interfaces;

/// <summary>
/// Contrato oficial de lectura de Empleados para otros módulos (EMP-BR-21, especificación §19), consumible en
/// proceso. Tenant y empresa salen siempre del contexto operacional (sin él, falla). No comprueba permisos:
/// cada consumidor aplica los suyos y no debe devolver nombres ni identificadores a un cliente sin
/// autorizarlo. No concede acceso al ERP ni incluye agenda, horarios o disponibilidad. Una ficha ajena se
/// comporta como inexistente.
/// </summary>
public interface IEmployeeDirectory
{
    /// <summary>Ficha de la empresa operativa (también en baja); nulo si no existe o es ajena.</summary>
    Task<EmployeeSummary?> GetByIdAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>
    /// Fichas de la empresa operativa, sin paginación. <paramref name="companyId"/> debe ser la empresa
    /// operativa (si no, lista vacía). Sin <paramref name="includeInactive"/> solo las que tienen un período
    /// abierto.
    /// </summary>
    Task<IReadOnlyList<EmployeeSummary>> ListAsync(
        Guid companyId,
        bool includeInactive,
        EmployeeDirectoryFilter? filter = null,
        CancellationToken ct = default
    );

    /// <summary>¿Hay un período laboral que cubra la fecha (inclusivo)? No usa el estado actual.</summary>
    Task<bool> IsActiveOnAsync(Guid employeeId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// Asignaciones de la ficha, opcionalmente de una sucursal y/o vigentes en una fecha; sin fecha, todo el
    /// historial. Vacío si la ficha no existe o es ajena.
    /// </summary>
    Task<IReadOnlyList<EmployeeAssignmentSummary>> GetBranchAssignmentsAsync(
        Guid employeeId,
        Guid? branchId = null,
        DateOnly? date = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// EMP-BR-12: período laboral que cubre la fecha <b>y</b> asignación a la sucursal que cubre la misma
    /// fecha. No mira el estado de la sucursal, el usuario, membresías, permisos ni el BusinessPartner: la
    /// habilitación operativa de la sucursal es del consumidor.
    /// </summary>
    Task<bool> IsEligibleForBranchAsync(
        Guid employeeId,
        Guid branchId,
        DateOnly date,
        CancellationToken ct = default
    );

    /// <summary>
    /// Usuario ERP vinculado (no indica acceso). Nulo sin vínculo, ficha ajena, cuenta global o sin membresía
    /// en la empresa; una membresía o cuenta inactiva conserva el identificador.
    /// </summary>
    Task<Guid?> GetLinkedUserAsync(Guid employeeId, CancellationToken ct = default);
}
