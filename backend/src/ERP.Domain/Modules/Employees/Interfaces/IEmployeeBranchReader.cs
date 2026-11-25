namespace ERP.Domain.Modules.Employees.Interfaces;

/// <summary>
/// Lectura de solo lectura de sucursales desde Empleados (EMP-E5, observación B). No modifica el módulo
/// de sucursales ni impide desactivarlas.
/// </summary>
public interface IEmployeeBranchReader
{
    /// <summary>
    /// ¿Existe la sucursal en el tenant y la empresa indicados y está activa? Los inexistentes, los de otra
    /// empresa o tenant y los inactivos son indistinguibles (<c>false</c>). Si está activa, queda bloqueada
    /// <c>FOR SHARE</c> hasta el fin de la transacción: una desactivación concurrente espera, y una ya
    /// confirmada se ve como inactiva. Requiere transacción activa.
    /// </summary>
    Task<bool> TryLockActiveAsync(Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct = default);
}
