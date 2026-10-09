using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.InitialLoad;

/// <inheritdoc cref="IInitialStockLookup"/>
public sealed class InitialStockLookup : IInitialStockLookup
{
    private readonly ErpDbContext _db;

    public InitialStockLookup(ErpDbContext db) => _db = db;

    public async Task<bool> HasStockHistoryAsync(Guid itemId, Guid warehouseId, CancellationToken ct) =>
        await _db.CurrentStocks.AsNoTracking()
            .AnyAsync(s => s.ProductId == itemId && s.WarehouseId == warehouseId, ct)
        || await _db.StockMovements.AsNoTracking()
            .AnyAsync(m => m.ProductId == itemId && m.WarehouseId == warehouseId, ct);
}
