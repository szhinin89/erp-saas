using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class InventarioStockRepository : IInventarioStockRepository
{
    private readonly ErpDbContext _context;

    public InventarioStockRepository(ErpDbContext context) => _context = context;

    public Task<StockActual?> GetStockByTenantBodegaProductAsync(
        Guid tenantId,
        Guid bodegaId,
        Guid productoId,
        CancellationToken ct = default)
        => _context.StockActual.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.BodegaId == bodegaId && s.ProductoId == productoId,
            ct);

    public async Task<IReadOnlyList<StockActual>> GetStockByTenantBodegaAsync(
        Guid tenantId,
        Guid bodegaId,
        Guid? productoId,
        CancellationToken ct = default)
    {
        var q = _context.StockActual
            .Where(s => s.TenantId == tenantId && s.BodegaId == bodegaId);

        if (productoId.HasValue)
            q = q.Where(s => s.ProductoId == productoId.Value);

        return await q.OrderBy(s => s.ProductoId).ToListAsync(ct);
    }

    public Task AddStockActualAsync(StockActual entity, CancellationToken ct = default)
        => _context.StockActual.AddAsync(entity, ct).AsTask();

    public Task AddMovimientoAsync(InventarioMovimiento movimiento, CancellationToken ct = default)
        => _context.InventarioMovimientos.AddAsync(movimiento, ct).AsTask();

    // ── Operaciones atómicas de concurrencia ─────────────────────────────────

    /// <inheritdoc/>
    public async Task<decimal?> DecrementarStockAtomicoAsync(
        Guid tenantId, Guid bodegaId, Guid productoId,
        decimal delta, Guid updatedBy,
        CancellationToken ct = default,
        decimal costoUnitario = 0m)
    {
        if (IsInMemoryProvider())
            return await DecrementarInMemoryAsync(tenantId, bodegaId, productoId, delta, updatedBy, ct, costoUnitario);

        var valorSalida = delta * costoUnitario;

        // PostgreSQL: una sola sentencia que verifica disponibilidad y descuenta.
        // También actualiza valor_total_stock para mantener el promedio ponderado.
        var rows = await _context.Database.ExecuteSqlAsync(
            $"""
            UPDATE stock_actual
            SET    cantidad             = cantidad - {delta},
                   valor_total_stock    = GREATEST(0, valor_total_stock - {valorSalida}),
                   ultima_actualizacion = NOW(),
                   updated_at           = NOW(),
                   updated_by           = {updatedBy}
            WHERE  tenant_id   = {tenantId}
              AND  bodega_id   = {bodegaId}
              AND  producto_id = {productoId}
              AND  (cantidad - cantidad_reservada) >= {delta}
            """, ct);

        if (rows == 0)
            return null; // stock insuficiente

        var tracked = _context.ChangeTracker.Entries<StockActual>()
            .FirstOrDefault(e =>
                e.Entity.TenantId   == tenantId  &&
                e.Entity.BodegaId   == bodegaId  &&
                e.Entity.ProductoId == productoId);

        decimal cantidadAnterior;
        if (tracked is not null)
        {
            cantidadAnterior = tracked.Entity.Cantidad;
            tracked.State = EntityState.Detached;
        }
        else
        {
            cantidadAnterior = 0;
        }

        return cantidadAnterior;
    }

    /// <inheritdoc/>
    public async Task<decimal> IncrementarStockAtomicoAsync(
        Guid tenantId, Guid bodegaId, Guid productoId,
        decimal delta, Guid createdBy,
        CancellationToken ct = default,
        decimal costoUnitario = 0m)
    {
        if (IsInMemoryProvider())
            return await IncrementarInMemoryAsync(tenantId, bodegaId, productoId, delta, createdBy, ct, costoUnitario);

        var tracked = _context.ChangeTracker.Entries<StockActual>()
            .FirstOrDefault(e =>
                e.Entity.TenantId   == tenantId  &&
                e.Entity.BodegaId   == bodegaId  &&
                e.Entity.ProductoId == productoId);

        var cantidadAnterior = tracked?.Entity.Cantidad ?? 0m;
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        var valorEntrada = delta * costoUnitario;
        var newId = Guid.NewGuid();

        // UPSERT: crea el registro si no existe, suma cantidad y valor_total_stock si ya existe.
        await _context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO stock_actual
                (id, tenant_id, producto_id, bodega_id,
                 cantidad, cantidad_reservada, valor_total_stock, ultima_actualizacion,
                 created_at, created_by, updated_at, updated_by)
            VALUES
                ({newId}, {tenantId}, {productoId}, {bodegaId},
                 {delta}, 0, {valorEntrada}, NOW(),
                 NOW(), {createdBy}, NOW(), {createdBy})
            ON CONFLICT (tenant_id, producto_id, bodega_id)
            DO UPDATE SET
                cantidad             = stock_actual.cantidad + EXCLUDED.cantidad,
                valor_total_stock    = stock_actual.valor_total_stock + EXCLUDED.valor_total_stock,
                ultima_actualizacion = NOW(),
                updated_at           = NOW(),
                updated_by           = EXCLUDED.updated_by
            """, ct);

        return cantidadAnterior;
    }

    public async Task<IReadOnlyList<InventarioMovimiento>> GetMovimientosAsync(
        Guid      tenantId,
        Guid      productoId,
        Guid      bodegaId,
        DateTime? desdeUtc,
        DateTime? hastaUtc,
        CancellationToken ct = default)
    {
        var q = _context.InventarioMovimientos
            .Where(m => m.TenantId == tenantId
                     && m.ProductoId == productoId
                     && m.BodegaId  == bodegaId);

        if (desdeUtc.HasValue)
            q = q.Where(m => m.CreatedAt >= desdeUtc.Value);
        if (hastaUtc.HasValue)
            q = q.Where(m => m.CreatedAt <= hastaUtc.Value);

        return await q.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync(ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // EF InMemory no soporta SQL raw; detectamos el proveedor por nombre.
    private bool IsInMemoryProvider()
        => _context.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

    // ── Fallbacks para EF InMemory (pruebas) ─────────────────────────────────

    private async Task<decimal?> DecrementarInMemoryAsync(
        Guid tenantId, Guid bodegaId, Guid productoId,
        decimal delta, Guid updatedBy, CancellationToken ct, decimal costoUnitario = 0m)
    {
        var stock = await _context.StockActual.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.BodegaId == bodegaId && s.ProductoId == productoId, ct);

        if (stock is null || stock.CantidadDisponible < delta)
            return null;

        var anterior = stock.Cantidad;
        stock.AplicarMovimiento(-delta, updatedBy, costoUnitario);
        return anterior;
    }

    private async Task<decimal> IncrementarInMemoryAsync(
        Guid tenantId, Guid bodegaId, Guid productoId,
        decimal delta, Guid createdBy, CancellationToken ct, decimal costoUnitario = 0m)
    {
        var stock = await _context.StockActual.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.BodegaId == bodegaId && s.ProductoId == productoId, ct);

        if (stock is null)
        {
            stock = StockActual.Create(tenantId, productoId, bodegaId, createdBy);
            await _context.StockActual.AddAsync(stock, ct);
            stock.AplicarMovimiento(delta, createdBy, costoUnitario);
            return 0;
        }

        var anterior = stock.Cantidad;
        stock.AplicarMovimiento(delta, createdBy, costoUnitario);
        return anterior;
    }
}
