using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class KardexSnapshotRepository : IKardexSnapshotRepository
{
    private readonly ErpDbContext _context;

    public KardexSnapshotRepository(ErpDbContext context) => _context = context;

    public Task<KardexSnapshot?> GetLatestBeforeAsync(
        Guid tenantId, Guid productoId, Guid bodegaId,
        DateTime hastaUtc, CancellationToken ct = default)
        => _context.KardexSnapshots
            .Where(s => s.TenantId   == tenantId
                     && s.ProductoId == productoId
                     && s.BodegaId   == bodegaId
                     && s.FechaSnapshot <= hastaUtc.Date)
            .OrderByDescending(s => s.FechaSnapshot)
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
            INSERT INTO kardex_snapshots
                (id, tenant_id, producto_id, bodega_id, fecha_snapshot,
                 cantidad_saldo, valor_saldo, costo_promedio, computado_en)
            VALUES
                ({snapshot.Id}, {snapshot.TenantId}, {snapshot.ProductoId}, {snapshot.BodegaId},
                 {snapshot.FechaSnapshot.Date}, {snapshot.CantidadSaldo}, {snapshot.ValorSaldo},
                 {snapshot.CostoPromedio}, {snapshot.ComputadoEn})
            ON CONFLICT (tenant_id, producto_id, bodega_id, fecha_snapshot)
            DO UPDATE SET
                cantidad_saldo = EXCLUDED.cantidad_saldo,
                valor_saldo    = EXCLUDED.valor_saldo,
                costo_promedio = EXCLUDED.costo_promedio,
                computado_en   = EXCLUDED.computado_en
            """, ct);
    }

    public async Task<IReadOnlyList<(Guid ProductoId, Guid BodegaId)>> GetDistinctProductoBodegaAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var rows = await _context.InventarioMovimientos
            .Where(m => m.TenantId == tenantId)
            .Select(m => new { m.ProductoId, m.BodegaId })
            .Distinct()
            .ToListAsync(ct);

        return rows.Select(r => (r.ProductoId, r.BodegaId)).ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetTenantsConMovimientosAsync(CancellationToken ct = default)
    {
        return await _context.InventarioMovimientos
            .Select(m => m.TenantId)
            .Distinct()
            .ToListAsync(ct);
    }

    // ── Fallback InMemory (tests) ─────────────────────────────────────────────

    private async Task UpsertInMemoryAsync(KardexSnapshot snap, CancellationToken ct)
    {
        var existing = await _context.KardexSnapshots.FirstOrDefaultAsync(
            s => s.TenantId   == snap.TenantId
              && s.ProductoId == snap.ProductoId
              && s.BodegaId   == snap.BodegaId
              && s.FechaSnapshot == snap.FechaSnapshot.Date, ct);

        if (existing is null)
        {
            await _context.KardexSnapshots.AddAsync(snap, ct);
        }
        else
        {
            // Actualizar en memoria via reflexión de propiedades privadas
            SetProp(existing, nameof(KardexSnapshot.CantidadSaldo), snap.CantidadSaldo);
            SetProp(existing, nameof(KardexSnapshot.ValorSaldo),    snap.ValorSaldo);
            SetProp(existing, nameof(KardexSnapshot.CostoPromedio), snap.CostoPromedio);
            SetProp(existing, nameof(KardexSnapshot.ComputadoEn),   snap.ComputadoEn);
        }

        await _context.SaveChangesAsync(ct);
    }

    private static void SetProp(object obj, string name, object value)
        => obj.GetType()
              .GetProperty(name,
                  System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
              .SetValue(obj, value);
}
