using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventory.UseCases.GetKardex;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.BackgroundServices;

/// <summary>
/// Procesa en segundo plano los reportes de Kardex encolados de forma asíncrona.
/// Lee IDs desde <see cref="KardexReportQueue"/>, instancia el handler con
/// <see cref="ManualCurrentTenant"/> (sin depender de HttpContext) y almacena
/// el resultado serializado en <c>kardex_reportes.resultado_json</c>.
/// </summary>
public sealed class KardexReportProcessor : BackgroundService
{
    private readonly KardexReportQueue               _queue;
    private readonly IServiceScopeFactory             _scopeFactory;
    private readonly ILogger<KardexReportProcessor>  _logger;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public KardexReportProcessor(
        KardexReportQueue              queue,
        IServiceScopeFactory            scopeFactory,
        ILogger<KardexReportProcessor> logger)
    {
        _queue        = queue;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KardexReportProcessor iniciado.");

        await foreach (var reporteId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try   { await ProcesarAsync(reporteId, stoppingToken); }
            catch (Exception ex)
            { _logger.LogError(ex, "Error inesperado procesando reporte {Id}", reporteId); }
        }

        _logger.LogInformation("KardexReportProcessor detenido.");
    }

    private async Task ProcesarAsync(Guid reporteId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformQueryAccessor>();

        var reporte = await platform
            .Unfiltered(db.KardexReports, PlatformQueryReason.BackgroundJob)
            .FirstOrDefaultAsync(r => r.Id == reporteId, ct);

        if (reporte is null)
        {
            _logger.LogWarning("KardexReportProcessor: reporte {Id} no encontrado.", reporteId);
            return;
        }

        _logger.LogInformation(
            "Procesando reporte {Id} (tenant={T}, prod={P}, Warehouse={B})",
            reporteId, reporte.TenantId, reporte.ProductId, reporte.WarehouseId);

        reporte.MarkProcessing();
        await db.SaveChangesAsync(ct);

        try
        {
            var kardex = scope.ServiceProvider.GetRequiredService<IKardexService>();
            var result = await kardex.GenerarKardexEscalableAsync(
                reporte.TenantId,
                new GetKardexQuery(reporte.ProductId, reporte.WarehouseId, reporte.DateFrom, reporte.DateTo),
                ct);

            if (result.IsSuccess)
                reporte.MarkCompleted(JsonSerializer.Serialize(result.Value, JsonOpts));
            else
                reporte.MarkError(result.Error ?? "Error desconocido.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al calcular kardex para reporte {Id}", reporteId);
            reporte.MarkError(ex.Message);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Reporte {Id}: estado final = {Estado}", reporteId, reporte.Status);
    }
}
