using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Employees;

/// <summary>
/// Locks transaccionales de Empleados (EMP-E3; los casos de uso de EMP-E4/E5 reutilizan este helper).
/// Mecanismo oficial: <c>pg_advisory_xact_lock(int4, int4)</c> con clave
/// (hash estable del namespace UTF-8, hash estable de los bytes de los Guid) — mismo algoritmo
/// <c>h = 17; h = h * 31 + b</c> que <c>AccountingOperationalPolicyRepository</c> y
/// <c>JournalEntryRepository</c>. Nunca <c>GetHashCode()</c> (no determinista entre procesos).
///
/// Orden de adquisición aprobado: Employees → BpEmployment (casos de uso de Empleados) y solo
/// BpEmployment (MasterData); no hay ciclo.
/// </summary>
public static class EmployeesAdvisoryLocks
{
    /// <summary>Namespace del lock por (tenant, BusinessPartner).</summary>
    public const string BpEmploymentNamespace = "BpEmployment";

    /// <summary>Namespace del lock por ficha (EMP-E4/E5).</summary>
    public const string EmployeesNamespace = "Employees";

    /// <summary>Clave <c>(int4, int4)</c> de <c>("BpEmployment", tenantId, businessPartnerId)</c>.</summary>
    public static (int Namespace, int Key) BpEmploymentKey(Guid tenantId, Guid businessPartnerId)
    {
        var bytes = new byte[32];
        tenantId.ToByteArray().CopyTo(bytes, 0);
        businessPartnerId.ToByteArray().CopyTo(bytes, 16);
        return (StableHash(System.Text.Encoding.UTF8.GetBytes(BpEmploymentNamespace)), StableHash(bytes));
    }

    /// <summary>Clave <c>(int4, int4)</c> de <c>("Employees", employeeId)</c>.</summary>
    public static (int Namespace, int Key) EmployeeKey(Guid employeeId) =>
        (
            StableHash(System.Text.Encoding.UTF8.GetBytes(EmployeesNamespace)),
            StableHash(employeeId.ToByteArray())
        );

    /// <summary>
    /// Lock exclusivo por (tenant, BP), de ámbito de transacción: se libera al COMMIT/ROLLBACK de la
    /// transacción ambiente y nunca abre ni confirma una propia. Sin transacción se liberaría al
    /// instante, así que se exige una activa.
    /// </summary>
    public static Task AcquireBpEmploymentAsync(
        ErpDbContext context,
        Guid tenantId,
        Guid businessPartnerId,
        CancellationToken cancellationToken
    ) =>
        AcquireAsync(
            context,
            BpEmploymentNamespace,
            BpEmploymentKey(tenantId, businessPartnerId),
            cancellationToken
        );

    /// <summary>
    /// Lock exclusivo por ficha (EMP-E4), con las mismas exigencias (transacción activa y
    /// READ COMMITTED). Cuando también se necesita BpEmployment, este se adquiere primero.
    /// </summary>
    public static Task AcquireEmployeeAsync(
        ErpDbContext context,
        Guid employeeId,
        CancellationToken cancellationToken
    ) => AcquireAsync(context, EmployeesNamespace, EmployeeKey(employeeId), cancellationToken);

    private static async Task AcquireAsync(
        ErpDbContext context,
        string lockName,
        (int Namespace, int Key) key,
        CancellationToken cancellationToken
    )
    {
        if (!context.Database.IsRelational())
            return;
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                $"El lock {lockName} requiere una transacción activa."
            );

        // El protocolo (lock → consulta → escritura) solo es correcto si cada sentencia ve lo
        // confirmado hasta ese momento: READ COMMITTED. Una transacción ambiental con otro
        // aislamiento (REPEATABLE READ / SERIALIZABLE fijan la instantánea en la primera consulta)
        // podría leer un estado anterior al lock, así que se rechaza; nunca se cambia el aislamiento
        // de una transacción ajena. Se valida ANTES de adquirir el lock.
        var isolation = await context
            .Database.SqlQuery<string>(
                $"SELECT current_setting('transaction_isolation') AS \"Value\""
            )
            .SingleAsync(cancellationToken);
        if (!string.Equals(isolation, "read committed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"El lock {lockName} exige una transacción READ COMMITTED (actual: {isolation})."
            );

        var (hash1, hash2) = key;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({hash1}, {hash2})",
            cancellationToken
        );
    }

    private static int StableHash(byte[] bytes)
    {
        var h = 17;
        foreach (var b in bytes)
            h = unchecked(h * 31 + b);
        return h;
    }
}
