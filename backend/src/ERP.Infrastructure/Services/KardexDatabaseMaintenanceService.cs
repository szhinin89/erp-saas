using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed partial class KardexDatabaseMaintenanceService : IKardexDatabaseMaintenance
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<KardexDatabaseMaintenanceService> _logger;

    public KardexDatabaseMaintenanceService(
        IConfiguration configuration,
        ILogger<KardexDatabaseMaintenanceService> logger)
    {
        _configuration = configuration;
        _logger        = logger;
    }

    public async Task RefreshDailyBalancesMaterializedViewAsync(CancellationToken cancellationToken = default)
    {
        var cs = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
        {
            LogNoConnectionString();
            return;
        }

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(cancellationToken);

        async Task ExecuteAsync(string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            await ExecuteAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY mv_saldos_diarios;");
            LogViewRefreshedConcurrently();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            LogViewNotYetExists();
        }
        catch (Exception ex)
        {
            LogConcurrentRefreshFailed(ex);
            try
            {
                await ExecuteAsync("REFRESH MATERIALIZED VIEW mv_saldos_diarios;");
                LogViewRefreshedBlocking();
            }
            catch (PostgresException ex2) when (ex2.SqlState == "42P01")
            {
                LogViewNotYetExists();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No hay DefaultConnection; no se refresca mv_saldos_diarios.")]
    private partial void LogNoConnectionString();

    [LoggerMessage(Level = LogLevel.Information, Message = "mv_saldos_diarios refrescada (CONCURRENTLY).")]
    private partial void LogViewRefreshedConcurrently();

    [LoggerMessage(Level = LogLevel.Information, Message = "mv_saldos_diarios no existe aún; aplique migraciones.")]
    private partial void LogViewNotYetExists();

    [LoggerMessage(Level = LogLevel.Information, Message = "REFRESH CONCURRENTLY no aplicó; se intenta REFRESH bloqueante.")]
    private partial void LogConcurrentRefreshFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "mv_saldos_diarios refrescada (bloqueante).")]
    private partial void LogViewRefreshedBlocking();
}
