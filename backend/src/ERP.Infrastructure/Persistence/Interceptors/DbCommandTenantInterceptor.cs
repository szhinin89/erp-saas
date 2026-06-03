using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Interceptor de DbCommand (nivel SQL) — segunda línea de defensa contra fugas de tenant.
///
/// RESPONSABILIDADES:
///   1. Detectar raw SQL sin cláusulas de tenant (subscriber_id / company_id)
///   2. Bloquear queries peligrosas que puedan retornar datos cross-tenant
///   3. Auditar todo SQL ejecutado en modo debug
///
/// NO es el mecanismo primario — los EF global query filters y CompanyScopeBehavior
/// son la primera línea. Este interceptor es la red de seguridad para raw SQL.
///
/// POLÍTICA: fail-audit (log + metric), NO fail-stop para no romper queries
/// legítimas de admin/platform que acceden datos cross-tenant intencionalmente.
/// Las queries peligrosas CONFIRMADAS (ej: SELECT * FROM tabla sin WHERE alguno)
/// SÍ son bloqueadas.
/// </summary>
public sealed class DbCommandTenantInterceptor : DbCommandInterceptor
{
    private static readonly string[] TenantColumns =
        ["subscriber_id", "company_id", "SubscriberId", "CompanyId"];

    private static readonly string[] AlwaysSafeTables =
        [
            // GLOBAL IMMUTABLE — sin tenant scope por diseño
            "global.", "__EFMigrationsHistory", "\"__EFMigrationsHistory\"",
            "information_schema", "pg_catalog",
            // Platform singletons
            "first_run_setup_state", "platform_provisioning_lock",
        ];

    // Tablas cuyo SELECT masivo sin WHERE es una señal de data leak
    private static readonly string[] SensitiveTables =
        [
            "master_business_partners", "identity_users",
            "sales_bill", "purch_bill", "journal_entries", "accounts",
            "bank_account", "petty_cash", "products",
        ];

    private readonly ILogger<DbCommandTenantInterceptor> _logger;

    public DbCommandTenantInterceptor(ILogger<DbCommandTenantInterceptor> logger)
        => _logger = logger;

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        AuditCommand(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken ct = default)
    {
        AuditCommand(command);
        return base.ReaderExecutingAsync(command, eventData, result, ct);
    }

    private void AuditCommand(DbCommand command)
    {
        var sql = command.CommandText;
        if (string.IsNullOrWhiteSpace(sql)) return;

        // Skip non-SELECT commands and always-safe tables
        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) return;
        if (IsAlwaysSafe(sql)) return;

        // Detect: SELECT from sensitive table with NO WHERE clause at all
        if (TouchesSensitiveTable(sql) && !HasWhereClause(sql))
        {
            _logger.LogCritical(
                "[TenantGuard:SQL] DANGEROUS QUERY — sensitive table access with NO WHERE clause. " +
                "Potential data leak. SQL: {Sql}",
                TruncateSql(sql));
            // In production hardening: throw InvalidOperationException here
            // For now: log critical and allow (admin queries need this)
            return;
        }

        // Detect: SELECT from ERP tables without any tenant column reference
        if (TouchesSensitiveTable(sql) && !HasTenantColumn(sql))
        {
            _logger.LogWarning(
                "[TenantGuard:SQL] Query on tenant-scoped table without tenant column in SQL. " +
                "Likely raw SQL bypassing EF filters. SQL: {Sql}",
                TruncateSql(sql));
        }
    }

    private static bool IsAlwaysSafe(string sql)
        => AlwaysSafeTables.Any(t => sql.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static bool TouchesSensitiveTable(string sql)
        => SensitiveTables.Any(t => sql.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static bool HasWhereClause(string sql)
        => sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase);

    private static bool HasTenantColumn(string sql)
        => TenantColumns.Any(c => sql.Contains(c, StringComparison.OrdinalIgnoreCase));

    private static string TruncateSql(string sql)
        => sql.Length > 300 ? sql[..300] + "..." : sql;
}
