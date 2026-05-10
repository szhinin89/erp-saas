using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ERP.Application.Inventario;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Interfaces;

namespace ERP.Infrastructure.BackgroundServices;

/// <summary>
/// Worker nocturno que genera snapshots diarios del kardex para todos los tenants.
/// Se ejecuta cada día a medianoche UTC.
///
/// Algoritmo por (tenant, producto, bodega):
///   1. Obtener el snapshot más reciente disponible.
///   2. Reproducir los movimientos desde ese snapshot hasta ayer (inclusive).
///   3. Guardar un snapshot por cada día procesado (acumulativo).
///
/// De esta forma el handler del kardex solo necesita leer movimientos DENTRO del rango
/// solicitado, sin recorrer todo el historial previo.
/// </summary>
public sealed class KardexSnapshotWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KardexSnapshotWorker> _logger;

    public KardexSnapshotWorker(
        IServiceScopeFactory scopeFactory,
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
            // Esperar hasta la próxima medianoche UTC (+ 1 min de margen para no adelantarse)
            var now          = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1).AddMinutes(1);
            var delay        = nextMidnight - now;

            _logger.LogDebug(
                "KardexSnapshotWorker: próxima ejecución en {Min:F0} minutos.", delay.TotalMinutes);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunAsync(stoppingToken);
        }

        _logger.LogInformation("KardexSnapshotWorker detenido.");
    }

    /// <summary>Ejecuta la generación de snapshots para todos los tenants.</summary>
    internal async Task RunAsync(CancellationToken ct)
    {
        var ayer = DateTime.UtcNow.Date.AddDays(-1);

        _logger.LogInformation("KardexSnapshotWorker: calculando snapshots para {Fecha:yyyy-MM-dd}.", ayer);

        using var scope          = _scopeFactory.CreateScope();
        var snapRepo  = scope.ServiceProvider.GetRequiredService<IKardexSnapshotRepository>();
        var movRepo   = scope.ServiceProvider.GetRequiredService<IInventarioStockRepository>();

        var tenants = await snapRepo.GetTenantsConMovimientosAsync(ct);

        var total = 0;
        var errores = 0;

        foreach (var tenantId in tenants)
        {
            if (ct.IsCancellationRequested) break;

            var combos = await snapRepo.GetDistinctProductoBodegaAsync(tenantId, ct);

            foreach (var (productoId, bodegaId) in combos)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await ProcesarComboAsync(
                        snapRepo, movRepo, tenantId, productoId, bodegaId, ayer, ct);
                    total++;
                }
                catch (Exception ex)
                {
                    errores++;
                    _logger.LogWarning(ex,
                        "Error al calcular snapshot tenant={T} producto={P} bodega={B}",
                        tenantId, productoId, bodegaId);
                }
            }
        }

        _logger.LogInformation(
            "KardexSnapshotWorker: {Total} snapshots calculados, {Err} errores.", total, errores);
    }

    // ── Cálculo para un (tenant, producto, bodega) ────────────────────────────

    private static async Task ProcesarComboAsync(
        IKardexSnapshotRepository snapRepo,
        IInventarioStockRepository movRepo,
        Guid tenantId, Guid productoId, Guid bodegaId,
        DateTime ayer, CancellationToken ct)
    {
        // Punto de partida: snapshot más reciente (puede ser null si es la primera vez)
        var ultimoSnap = await snapRepo.GetLatestBeforeAsync(
            tenantId, productoId, bodegaId, ayer, ct);

        decimal saldoCantidad = ultimoSnap?.CantidadSaldo ?? 0m;
        decimal saldoValor    = ultimoSnap?.ValorSaldo    ?? 0m;
        decimal costoPromedio = ultimoSnap?.CostoPromedio ?? 0m;

        // Fecha desde la que hay que calcular (día siguiente al último snapshot disponible)
        var desdeUtc = ultimoSnap is null
            ? (DateTime?)null
            : ultimoSnap.FechaSnapshot.AddDays(1);

        // Traer solo movimientos posteriores al último snapshot y hasta ayer (inclusive)
        var hastaUtc = ayer.AddDays(1).AddTicks(-1); // fin de 'ayer' UTC
        var movs = await movRepo.GetMovimientosAsync(
            tenantId, productoId, bodegaId, desdeUtc, hastaUtc, ct);

        if (movs.Count == 0 && ultimoSnap?.FechaSnapshot == ayer)
            return; // ya existe el snapshot de ayer y no hay nuevos movimientos

        // Agrupar movimientos por día y calcular snapshot acumulado día a día.
        // Esto genera un snapshot por cada día con actividad (y uno para ayer si no hay actividad
        // pero hay saldo que reportar).
        var porDia = movs
            .GroupBy(m => m.CreatedAt.Date)
            .OrderBy(g => g.Key);

        DateTime? ultimoDiaConMovs = null;

        foreach (var grupo in porDia)
        {
            foreach (var m in grupo.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
                KardexCalculator.AplicarMovimiento(m, ref saldoCantidad, ref saldoValor, ref costoPromedio);

            var snap = KardexSnapshot.Create(
                tenantId, productoId, bodegaId, grupo.Key,
                saldoCantidad, saldoValor, costoPromedio);

            await snapRepo.UpsertAsync(snap, ct);
            ultimoDiaConMovs = grupo.Key;
        }

        // Si el último día con movimientos es anterior a ayer, guardar también snapshot de ayer
        // para que las consultas que arrancan HOY tengan un punto de partida válido.
        if (ultimoDiaConMovs != ayer)
        {
            var snapAyer = KardexSnapshot.Create(
                tenantId, productoId, bodegaId, ayer,
                saldoCantidad, saldoValor, costoPromedio);
            await snapRepo.UpsertAsync(snapAyer, ct);
        }
    }
}
