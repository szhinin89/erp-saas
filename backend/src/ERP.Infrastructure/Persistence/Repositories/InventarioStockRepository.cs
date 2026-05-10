using Microsoft.EntityFrameworkCore;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Interfaces;

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
        decimal delta, Guid updatedBy, CancellationToken ct = default)
    {
        if (IsInMemoryProvider())
            return await DecrementarInMemoryAsync(tenantId, bodegaId, productoId, delta, updatedBy, ct);

        // PostgreSQL: una sola sentencia que verifica disponibilidad y descuenta.
        // Si afecta 0 filas → stock insuficiente (posiblemente modificado concurrentemente).
        var rows = await _context.Database.ExecuteSqlAsync(
            $"""
            UPDATE stock_actual
            SET    cantidad             = cantidad - {delta},
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

        // La cantidad anterior fue (cantidad_actual_en_db + delta).
        // La recuperamos del change tracker si la entidad fue pre-cargada.
        var tracked = _context.ChangeTracker.Entries<StockActual>()
            .FirstOrDefault(e =>
                e.Entity.TenantId   == tenantId  &&
                e.Entity.BodegaId   == bodegaId  &&
                e.Entity.ProductoId == productoId);

        decimal cantidadAnterior;
        if (tracked is not null)
        {
            // El valor en memoria es el que había antes del UPDATE SQL.
            cantidadAnterior = tracked.Entity.Cantidad;
            // Desenganchamos la entidad para que EF no intente sobreescribir el UPDATE atómico.
            tracked.State = EntityState.Detached;
        }
        else
        {
            // No estaba en el tracker (lectura sin tracking previa).
            // Aproximación: no conocemos el anterior exacto, registramos 0.
            cantidadAnterior = 0;
        }

        return cantidadAnterior;
    }

    /// <inheritdoc/>
    public async Task<decimal> IncrementarStockAtomicoAsync(
        Guid tenantId, Guid bodegaId, Guid productoId,
        decimal delta, Guid createdBy, CancellationToken ct = default)
    {
        if (IsInMemoryProvider())
            return await IncrementarInMemoryAsync(tenantId, bodegaId, productoId, delta, createdBy, ct);

        // Capturar cantidadAnterior desde el tracker antes del UPSERT
        var tracked = _context.ChangeTracker.Entries<StockActual>()
            .FirstOrDefault(e =>
                e.Entity.TenantId   == tenantId  &&
                e.Entity.BodegaId   == bodegaId  &&
                e.Entity.ProductoId == productoId);

        var cantidadAnterior = tracked?.Entity.Cantidad ?? 0m;
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        // PostgreSQL: UPSERT sobre el índice único (tenant_id, producto_id, bodega_id).
        // Si no existe el registro lo crea; si existe, suma el delta.
        var newId = Guid.NewGuid();
        await _context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO stock_actual
                (id, tenant_id, producto_id, bodega_id,
                 cantidad, cantidad_reservada, ultima_actualizacion,
                 created_at, created_by, updated_at, updated_by)
            VALUES
                ({newId}, {tenantId}, {productoId}, {bodegaId},
                 {delta}, 0, NOW(),
                 NOW(), {createdBy}, NOW(), {createdBy})
            ON CONFLICT (tenant_id, producto_id, bodega_id)
            DO UPDATE SET
                cantidad             = stock_actual.cantidad + EXCLUDED.cantidad,
                ultima_actualizacion = NOW(),
                updated_at           = NOW(),
                updated_by           = EXCLUDED.updated_by
            """, ct);

        return cantidadAnterior;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // EF InMemory no soporta SQL raw; detectamos el proveedor por nombre.
    private bool IsInMemoryProvider()
        => _context.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

    // ── Fallbacks para EF InMemory (pruebas) ─────────────────────────────────

    private async Task<decimal?> DecrementarInMemoryAsync(
        Guid tenantId, Guid bodegaId, Guid productoId,
        decimal delta, Guid updatedBy, CancellationToken ct)
    {
        var stock = await _context.StockActual.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.BodegaId == bodegaId && s.ProductoId == productoId, ct);

        if (stock is null || stock.CantidadDisponible < delta)
            return null;

        var anterior = stock.Cantidad;
        stock.AplicarMovimiento(-delta, updatedBy);
        return anterior;
    }

    private async Task<decimal> IncrementarInMemoryAsync(
        Guid tenantId, Guid bodegaId, Guid productoId,
        decimal delta, Guid createdBy, CancellationToken ct)
    {
        var stock = await _context.StockActual.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.BodegaId == bodegaId && s.ProductoId == productoId, ct);

        if (stock is null)
        {
            stock = StockActual.Create(tenantId, productoId, bodegaId, createdBy);
            await _context.StockActual.AddAsync(stock, ct);
            stock.AplicarMovimiento(delta, createdBy);
            return 0;
        }

        var anterior = stock.Cantidad;
        stock.AplicarMovimiento(delta, createdBy);
        return anterior;
    }
}
