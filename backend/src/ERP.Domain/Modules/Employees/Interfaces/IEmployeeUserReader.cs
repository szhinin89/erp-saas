namespace ERP.Domain.Modules.Employees.Interfaces;

/// <summary>
/// Lectura de membresías desde Empleados (EMP-E6). No modifica Access: solo bloquea la fila para que la
/// vinculación se decida sobre un estado estable. La elegibilidad (activa, no global) la resuelve la regla
/// única de IAM (<c>IamIdentityScope</c>).
/// </summary>
public interface IEmployeeUserReader
{
    /// <summary>
    /// Bloquea <c>FOR SHARE</c> la membresía de la identidad en la empresa del tenant, si existe, hasta el
    /// fin de la transacción: una revocación concurrente espera, y una ya confirmada se verá en la lectura
    /// posterior. <c>false</c> si no hay membresía en ese tenant y empresa. Requiere transacción activa.
    /// </summary>
    Task<bool> TryLockMembershipAsync(
        Guid tenantId,
        Guid companyId,
        Guid identityUserId,
        CancellationToken ct = default
    );
}
