using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class StockRepository : IStockRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public StockRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<CurrentStock> Scoped(Guid subscriberId)
        => _context.CurrentStocks.ForOperationalScope(subscriberId, _company);

    public Task<CurrentStock?> GetStockAsync(
        Guid subscriberId,
        Guid WarehouseId,
        Guid productoId,
        CancellationToken ct = default)
        => Scoped(subscriberId).FirstOrDefaultAsync(
            s => s.WarehouseId == WarehouseId && s.ProductId == productoId,
            ct);

    public async Task<IReadOnlyList<CurrentStock>> GetStockByWarehouseAsync(
        Guid subscriberId,
        Guid WarehouseId,
        Guid? productoId,
        CancellationToken ct = default)
    {
        var q = Scoped(subscriberId).Where(s => s.WarehouseId == WarehouseId);

        if (productoId.HasValue)
            q = q.Where(s => s.ProductId == productoId.Value);

        return await q.OrderBy(s => s.ProductId).ToListAsync(ct);
    }

    public Task AddCurrentStockAsync(CurrentStock entity, CancellationToken ct = default)
        => _context.CurrentStocks.AddAsync(entity, ct).AsTask();

    public Task AddMovementAsync(StockMovement movimiento, CancellationToken ct = default)
        => _context.StockMovements.AddAsync(movimiento, ct).AsTask();

    // â”€â”€ Operaciones atÃ³micas de concurrencia â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <inheritdoc/>
    public async Task<decimal?> DecrementStockAtomicAsync(
        Guid subscriberId, Guid WarehouseId, Guid productoId,
        decimal delta, Guid updatedBy,
        CancellationToken ct = default,
        decimal unitCost = 0m)
    {
        if (IsInMemoryProvider())
            return await DecrementarInMemoryAsync(subscriberId, WarehouseId, productoId, delta, updatedBy, ct, unitCost);

        var valorSalida = delta * unitCost;

        // PostgreSQL: una sola sentencia que verifica disponibilidad y descuenta.
        // TambiÃ©n actualiza valor_total_stock para mantener el promedio ponderado.
        var rows = await _context.Database.ExecuteSqlAsync(
            $"""
            UPDATE stock_actual
            SET    cantidad             = cantidad - {delta},
                   valor_total_stock    = GREATEST(0, valor_total_stock - {valorSalida}),
                   ultima_actualizacion = NOW(),
                   updated_at           = NOW(),
                   updated_by           = {updatedBy}
            WHERE  subscriber_id   = {subscriberId}
              AND  Warehouse_id   = {WarehouseId}
              AND  producto_id = {productoId}
              AND  (cantidad - cantidad_reservada) >= {delta}
            """, ct);

        if (rows == 0)
            return null; // stock insuficiente

        var tracked = _context.ChangeTracker.Entries<CurrentStock>()
            .FirstOrDefault(e =>
                e.Entity.SubscriberId   == subscriberId  &&
                e.Entity.WarehouseId   == WarehouseId  &&
                e.Entity.ProductId == productoId);

        decimal cantidadAnterior;
        if (tracked is not null)
        {
            cantidadAnterior = tracked.Entity.Quantity;
            tracked.State = EntityState.Detached;
        }
        else
        {
            cantidadAnterior = 0;
        }

        return cantidadAnterior;
    }

    /// <inheritdoc/>
    public async Task<decimal> IncrementStockAtomicAsync(
        Guid subscriberId, Guid WarehouseId, Guid productoId,
        decimal delta, Guid createdBy,
        CancellationToken ct = default,
        decimal unitCost = 0m)
    {
        if (IsInMemoryProvider())
            return await IncrementarInMemoryAsync(subscriberId, WarehouseId, productoId, delta, createdBy, ct, unitCost);

        var tracked = _context.ChangeTracker.Entries<CurrentStock>()
            .FirstOrDefault(e =>
                e.Entity.SubscriberId   == subscriberId  &&
                e.Entity.WarehouseId   == WarehouseId  &&
                e.Entity.ProductId == productoId);

        var cantidadAnterior = tracked?.Entity.Quantity ?? 0m;
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        var valorEntrada = delta * unitCost;
        var newId = Guid.NewGuid();

        // UPSERT: crea el registro si no existe, suma cantidad y valor_total_stock si ya existe.
        await _context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO stock_actual
                (id, subscriber_id, producto_id, Warehouse_id,
                 cantidad, cantidad_reservada, valor_total_stock, ultima_actualizacion,
                 created_at, created_by, updated_at, updated_by)
            VALUES
                ({newId}, {subscriberId}, {productoId}, {WarehouseId},
                 {delta}, 0, {valorEntrada}, NOW(),
                 NOW(), {createdBy}, NOW(), {createdBy})
            ON CONFLICT (subscriber_id, producto_id, Warehouse_id)
            DO UPDATE SET
                cantidad             = stock_actual.cantidad + EXCLUDED.cantidad,
                valor_total_stock    = stock_actual.valor_total_stock + EXCLUDED.valor_total_stock,
                ultima_actualizacion = NOW(),
                updated_at           = NOW(),
                updated_by           = EXCLUDED.updated_by
            """, ct);

        return cantidadAnterior;
    }

    public async Task<IReadOnlyList<StockMovement>> GetMovementsAsync(
        Guid      subscriberId,
        Guid      productoId,
        Guid      WarehouseId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var q = _context.StockMovements
            .Where(m => m.SubscriberId == subscriberId
                     && m.ProductId == productoId
                     && m.WarehouseId  == WarehouseId);

        if (fromUtc.HasValue)
            q = q.Where(m => m.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue)
            q = q.Where(m => m.CreatedAt <= toUtc.Value);

        return await q.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync(ct);
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // EF InMemory no soporta SQL raw; detectamos el Supplier por nombre.
    private bool IsInMemoryProvider()
        => _context.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

    // â”€â”€ Fallbacks para EF InMemory (pruebas) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async Task<decimal?> DecrementarInMemoryAsync(
        Guid subscriberId, Guid WarehouseId, Guid productoId,
        decimal delta, Guid updatedBy, CancellationToken ct, decimal unitCost = 0m)
    {
        var stock = await _context.CurrentStocks.FirstOrDefaultAsync(
            s => s.SubscriberId == subscriberId && s.WarehouseId == WarehouseId && s.ProductId == productoId, ct);

        if (stock is null || stock.AvailableQuantity < delta)
            return null;

        var anterior = stock.Quantity;
        stock.ApplyMovement(-delta, updatedBy, unitCost);
        return anterior;
    }

    private async Task<decimal> IncrementarInMemoryAsync(
        Guid subscriberId, Guid WarehouseId, Guid productoId,
        decimal delta, Guid createdBy, CancellationToken ct, decimal unitCost = 0m)
    {
        var stock = await _context.CurrentStocks.FirstOrDefaultAsync(
            s => s.SubscriberId == subscriberId && s.WarehouseId == WarehouseId && s.ProductId == productoId, ct);

        if (stock is null)
        {
            var companyId = _company.CompanyId;
            stock = CurrentStock.Create(subscriberId, productoId, WarehouseId, createdBy, companyId: companyId);
            await _context.CurrentStocks.AddAsync(stock, ct);
            stock.ApplyMovement(delta, createdBy, unitCost);
            return 0;
        }

        var anterior = stock.Quantity;
        stock.ApplyMovement(delta, createdBy, unitCost);
        return anterior;
    }
}
