using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventario.UseCases.GetKardex;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.BackgroundServices;

/// <summary>
/// Procesa en segundo plano los reportes de Kardex encolados de forma asíncrona.
/// Lee IDs desde <see cref="KardexReporteQueue"/>, instancia el handler con
/// <see cref="ManualCurrentTenant"/> (sin depender de HttpContext) y almacena
/// el resultado serializado en <c>kardex_reportes.resultado_json</c>.
/// </summary>
public sealed class KardexReporteProcessor : BackgroundService
{
    private readonly KardexReporteQueue               _queue;
    private readonly IServiceScopeFactory             _scopeFactory;
    private readonly ILogger<KardexReporteProcessor>  _logger;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public KardexReporteProcessor(
        KardexReporteQueue              queue,
        IServiceScopeFactory            scopeFactory,
        ILogger<KardexReporteProcessor> logger)
    {
        _queue        = queue;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KardexReporteProcessor iniciado.");

        await foreach (var reporteId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try   { await ProcesarAsync(reporteId, stoppingToken); }
            catch (Exception ex)
            { _logger.LogError(ex, "Error inesperado procesando reporte {Id}", reporteId); }
        }

        _logger.LogInformation("KardexReporteProcessor detenido.");
    }

    private async Task ProcesarAsync(Guid reporteId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        // IQF: el DbContext del worker no tiene tenant HTTP; el aislamiento viene de la fila (Id único global)
        // y luego se impone ManualCurrentTenant(reporte.TenantId) antes de tocar inventario.
        var reporte = await db.KardexReportes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == reporteId, ct);

        if (reporte is null)
        {
            _logger.LogWarning("KardexReporteProcessor: reporte {Id} no encontrado.", reporteId);
            return;
        }

        _logger.LogInformation(
            "Procesando reporte {Id} (tenant={T}, prod={P}, bodega={B})",
            reporteId, reporte.TenantId, reporte.ProductoId, reporte.BodegaId);

        reporte.MarcarProcesando();
        await db.SaveChangesAsync(ct);

        try
        {
            var kardex = scope.ServiceProvider.GetRequiredService<IKardexService>();
            var result = await kardex.GenerarKardexEscalableAsync(
                reporte.TenantId,
                new GetKardexQuery(reporte.ProductoId, reporte.BodegaId, reporte.FechaInicio, reporte.FechaFin),
                ct);

            if (result.IsSuccess)
                reporte.MarcarCompletado(JsonSerializer.Serialize(result.Value, JsonOpts));
            else
                reporte.MarcarError(result.Error ?? "Error desconocido.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al calcular kardex para reporte {Id}", reporteId);
            reporte.MarcarError(ex.Message);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Reporte {Id}: estado final = {Estado}", reporteId, reporte.Estado);
    }
}
