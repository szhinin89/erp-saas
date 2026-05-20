using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ERP.Infrastructure.Services;

namespace ERP.Infrastructure.BackgroundServices;

/// <summary>
/// Worker nocturno que genera snapshots diarios del kardex para todos los subscribers.
/// Se activa cada día a medianoche UTC y delega el cálculo a <see cref="KardexSnapshotService"/>.
/// </summary>
public sealed class KardexSnapshotWorker : BackgroundService
{
    private readonly IServiceScopeFactory          _scopeFactory;
    private readonly ILogger<KardexSnapshotWorker> _logger;

    public KardexSnapshotWorker(
        IServiceScopeFactory          scopeFactory,
        ILogger<KardexSnapshotWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KardexSnapshotWorker iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Esperar hasta la próxima medianoche UTC (+ 1 min de margen)
            var now          = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1).AddMinutes(1);

            _logger.LogDebug(
                "KardexSnapshotWorker: próxima ejecución en {Min:F0} minutos.",
                (nextMidnight - now).TotalMinutes);

            try { await Task.Delay(nextMidnight - now, stoppingToken); }
            catch (OperationCanceledException) { break; }

            await RunAsync(stoppingToken);
        }

        _logger.LogInformation("KardexSnapshotWorker detenido.");
    }

    internal async Task RunAsync(CancellationToken ct)
    {
        var ayer = DateTime.UtcNow.Date.AddDays(-1);
        _logger.LogInformation("KardexSnapshotWorker: calculando snapshots para {Fecha:yyyy-MM-dd}.", ayer);

        using var scope   = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<KardexSnapshotService>();

        var total = await service.RecalcularTodosAsync(ayer, ct);

        _logger.LogInformation("KardexSnapshotWorker: {Total} snapshots calculados.", total);
    }
}
