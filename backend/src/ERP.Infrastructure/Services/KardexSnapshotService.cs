using Microsoft.Extensions.Logging;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventory;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Servicio de cómputo de snapshots del kardex.
/// Usado por el <c>KardexSnapshotWorker</c> (nocturno) y por el
/// <c>RecalcularSnapshotsCommandHandler</c> (bajo demanda).
/// </summary>
public sealed class KardexSnapshotService : IKardexSnapshotCalculator
{
    private readonly IKardexSnapshotRepository  _snapRepo;
    private readonly IStockRepository _movRepo;
    private readonly ILogger<KardexSnapshotService> _logger;

    public KardexSnapshotService(
        IKardexSnapshotRepository  snapRepo,
        IStockRepository movRepo,
        ILogger<KardexSnapshotService> logger)
    {
        _snapRepo = snapRepo;
        _movRepo  = movRepo;
        _logger   = logger;
    }

    // ── API pública ──────────────────────────────────────────────────────────

    /// <summary>
    /// Recalcula snapshots para todos los tenants hasta <paramref name="hastaFecha"/>.
    /// </summary>
    public async Task<int> RecalcularTodosAsync(DateTime hastaFecha, CancellationToken ct)
    {
        var tenants = await _snapRepo.GetTenantsWithMovementsAsync(ct);
        var total   = 0;

        foreach (var tenantId in tenants)
        {
            if (ct.IsCancellationRequested) break;
            total += await RecalcularTenantAsync(tenantId, null, null, hastaFecha, ct);
        }

        return total;
    }

    /// <summary>
    /// Recalcula snapshots para un tenant específico, con filtros opcionales.
    /// </summary>
    public async Task<int> RecalcularTenantAsync(
        Guid      tenantId,
        Guid?     productoId,
        Guid?     WarehouseId,
        DateTime  hastaFecha,
        CancellationToken ct)
    {
        IReadOnlyList<(Guid ProductoId, Guid WarehouseId)> combos;

        if (productoId.HasValue && WarehouseId.HasValue)
            combos = [(productoId.Value, WarehouseId.Value)];
        else
        {
            var todos = await _snapRepo.GetDistinctProductWarehouseAsync(tenantId, ct);
            combos = productoId.HasValue
                ? todos.Where(c => c.ProductId == productoId.Value).ToList()
                : WarehouseId.HasValue
                    ? todos.Where(c => c.WarehouseId == WarehouseId.Value).ToList()
                    : todos;
        }

        var total = 0;

        foreach (var (pid, bid) in combos)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var count = await ProcesarComboAsync(tenantId, pid, bid, hastaFecha, ct);
                total += count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error al calcular snapshot tenant={T} producto={P} Warehouse={B}",
                    tenantId, pid, bid);
            }
        }

        return total;
    }

    // ── Cálculo por combinación (producto × Warehouse) ──────────────────────────

    private async Task<int> ProcesarComboAsync(
        Guid tenantId, Guid productoId, Guid WarehouseId,
        DateTime hastaFecha, CancellationToken ct)
    {
        var ayer = hastaFecha.Date;

        // Punto de partida: snapshot más reciente disponible
        var ultimoSnap = await _snapRepo.GetLatestBeforeAsync(
            tenantId, productoId, WarehouseId, ayer, ct);

        decimal saldoCantidad = ultimoSnap?.BalanceQty ?? 0m;
        decimal saldoValor    = ultimoSnap?.BalanceValue    ?? 0m;
        decimal costoPromedio = ultimoSnap?.AverageCost ?? 0m;

        var desdeUtc  = ultimoSnap is null
            ? (DateTime?)null
            : ultimoSnap.SnapshotDate.AddDays(1);
        var hastaUtc  = ayer.AddDays(1).AddTicks(-1);

        var movs = await _movRepo.GetMovementsAsync(
            tenantId, productoId, WarehouseId, desdeUtc, hastaUtc, ct);

        if (movs.Count == 0 && ultimoSnap?.SnapshotDate.Date == ayer)
            return 0; // snapshot de ayer ya existe y sin nuevos movimientos

        // Calcular snapshot acumulado día a día
        var porDia = movs
            .GroupBy(m => m.CreatedAt.Date)
            .OrderBy(g => g.Key);

        var snapshotsGuardados = 0;
        DateTime? ultimoDiaConMovs = null;

        foreach (var grupo in porDia)
        {
            foreach (var m in grupo.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
                KardexCalculator.ApplyMovement(m, ref saldoCantidad, ref saldoValor, ref costoPromedio);

            var snap = KardexSnapshot.Create(
                tenantId, productoId, WarehouseId, grupo.Key,
                saldoCantidad, saldoValor, costoPromedio);

            await _snapRepo.UpsertAsync(snap, ct);
            ultimoDiaConMovs = grupo.Key;
            snapshotsGuardados++;
        }

        // Guardar snapshot de ayer si no tiene movimientos propios (saldo se hereda)
        if (ultimoDiaConMovs != ayer)
        {
            var snapAyer = KardexSnapshot.Create(
                tenantId, productoId, WarehouseId, ayer,
                saldoCantidad, saldoValor, costoPromedio);
            await _snapRepo.UpsertAsync(snapAyer, ct);
            snapshotsGuardados++;
        }

        return snapshotsGuardados;
    }
}
