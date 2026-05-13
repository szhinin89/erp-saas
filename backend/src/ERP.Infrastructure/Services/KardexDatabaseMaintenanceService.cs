using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class KardexDatabaseMaintenanceService : IKardexDatabaseMaintenance
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
            _logger.LogWarning("No hay DefaultConnection; no se refresca mv_saldos_diarios.");
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
            _logger.LogInformation("mv_saldos_diarios refrescada (CONCURRENTLY).");
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogInformation("mv_saldos_diarios no existe aún; aplique migraciones.");
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "REFRESH CONCURRENTLY no aplicó; se intenta REFRESH bloqueante.");
            try
            {
                await ExecuteAsync("REFRESH MATERIALIZED VIEW mv_saldos_diarios;");
                _logger.LogInformation("mv_saldos_diarios refrescada (bloqueante).");
            }
            catch (PostgresException ex2) when (ex2.SqlState == "42P01")
            {
                _logger.LogInformation("mv_saldos_diarios no existe aún; aplique migraciones.");
            }
        }
    }
}
