using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class KardexSnapshotRepository : IKardexSnapshotRepository
{
    private readonly ErpDbContext _context;

    public KardexSnapshotRepository(ErpDbContext context) => _context = context;

    public Task<KardexSnapshot?> GetLatestBeforeAsync(
        Guid subscriberId, Guid productoId, Guid WarehouseId,
        DateTime toUtc, CancellationToken ct = default)
        => _context.KardexSnapshots
            .Where(s => s.SubscriberId   == subscriberId
                     && s.ProductId == productoId
                     && s.WarehouseId   == WarehouseId
                     && s.SnapshotDate <= toUtc.Date)
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefaultAsync(ct);

    public async Task UpsertAsync(KardexSnapshot snapshot, CancellationToken ct = default)
    {
        // PostgreSQL: INSERT … ON CONFLICT DO UPDATE
        if (_context.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            await UpsertInMemoryAsync(snapshot, ct);
            return;
        }

        await _context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO kardex_snapshot
                (id, subscriber_id, product_id, warehouse_id, snapshot_date,
                 balance_qty, balance_value, average_cost, computed_at)
            VALUES
                ({snapshot.Id}, {snapshot.SubscriberId}, {snapshot.ProductId}, {snapshot.WarehouseId},
                 {snapshot.SnapshotDate.Date}, {snapshot.BalanceQty}, {snapshot.BalanceValue},
                 {snapshot.AverageCost}, {snapshot.ComputedAt})
            ON CONFLICT (subscriber_id, product_id, warehouse_id, snapshot_date)
            DO UPDATE SET
                balance_qty   = EXCLUDED.balance_qty,
                balance_value = EXCLUDED.balance_value,
                average_cost  = EXCLUDED.average_cost,
                computed_at   = EXCLUDED.computed_at
            """, ct);
    }

    public async Task<IReadOnlyList<(Guid ProductId, Guid WarehouseId)>> GetDistinctProductWarehouseAsync(
        Guid subscriberId, CancellationToken ct = default)
    {
        var rows = await _context.StockMovements
            .Where(m => m.SubscriberId == subscriberId)
            .Select(m => new { m.ProductId, m.WarehouseId })
            .Distinct()
            .ToListAsync(ct);

        return rows.Select(r => (r.ProductId, r.WarehouseId)).ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetTenantsWithMovementsAsync(CancellationToken ct = default)
    {
        return await _context.StockMovements
            .Select(m => m.SubscriberId)
            .Distinct()
            .ToListAsync(ct);
    }

    // ── Fallback InMemory (tests) ─────────────────────────────────────────────

    private async Task UpsertInMemoryAsync(KardexSnapshot snap, CancellationToken ct)
    {
        var existing = await _context.KardexSnapshots.FirstOrDefaultAsync(
            s => s.SubscriberId   == snap.SubscriberId
              && s.ProductId == snap.ProductId
              && s.WarehouseId   == snap.WarehouseId
              && s.SnapshotDate == snap.SnapshotDate.Date, ct);

        if (existing is null)
        {
            await _context.KardexSnapshots.AddAsync(snap, ct);
        }
        else
        {
            // Actualizar en memoria via reflexión de propiedades privadas
            SetProp(existing, nameof(KardexSnapshot.BalanceQty), snap.BalanceQty);
            SetProp(existing, nameof(KardexSnapshot.BalanceValue),    snap.BalanceValue);
            SetProp(existing, nameof(KardexSnapshot.AverageCost), snap.AverageCost);
            SetProp(existing, nameof(KardexSnapshot.ComputedAt),   snap.ComputedAt);
        }

        await _context.SaveChangesAsync(ct);
    }

    private static void SetProp(object obj, string name, object value)
        => obj.GetType()
              .GetProperty(name,
                  System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
              .SetValue(obj, value);
}
