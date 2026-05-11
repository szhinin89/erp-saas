using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Services;

public sealed class KardexMaterializedDailySummariesReader : IKardexMaterializedDailySummariesReader
{
    private readonly ErpDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KardexMaterializedDailySummariesReader> _logger;

    public KardexMaterializedDailySummariesReader(
        ErpDbContext db,
        IConfiguration configuration,
        ILogger<KardexMaterializedDailySummariesReader> logger)
    {
        _db            = db;
        _configuration = configuration;
        _logger        = logger;
    }

    public async Task<IReadOnlyList<KardexMvDayAggregate>?> TryGetDailyAggregatesAsync(
        Guid tenantId,
        Guid productoId,
        Guid bodegaId,
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                _db.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
            return null;

        var cs = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
            return null;

        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new NpgsqlCommand(
                """
                SELECT fecha,
                       entradas_cantidad,
                       entradas_valor,
                       salidas_cantidad,
                       salidas_valor
                FROM mv_saldos_diarios
                WHERE tenant_id = @t
                  AND producto_id = @p
                  AND bodega_id = @b
                  AND fecha >= @d0
                  AND fecha <= @d1
                ORDER BY fecha;
                """,
                conn);

            cmd.Parameters.AddWithValue("t",  tenantId);
            cmd.Parameters.AddWithValue("p",  productoId);
            cmd.Parameters.AddWithValue("b",  bodegaId);
            cmd.Parameters.AddWithValue("d0", fromInclusive);
            cmd.Parameters.AddWithValue("d1", toInclusive);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<KardexMvDayAggregate>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var fecha = reader.GetFieldType(0) == typeof(DateTime)
                    ? DateOnly.FromDateTime(reader.GetDateTime(0))
                    : reader.GetFieldValue<DateOnly>(0);

                list.Add(new KardexMvDayAggregate(
                    fecha,
                    reader.GetDecimal(1),
                    reader.GetDecimal(2),
                    reader.GetDecimal(3),
                    reader.GetDecimal(4)));
            }

            return list;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogDebug(ex, "mv_saldos_diarios no disponible.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lectura de mv_saldos_diarios omitida; se usarán movimientos detallados.");
            return null;
        }
    }
}

/// <summary>Códigos SQLSTATE PostgreSQL usados en el reader.</summary>
file static class PostgresErrorCodes
{
    public const string UndefinedTable = "42P01";
}
