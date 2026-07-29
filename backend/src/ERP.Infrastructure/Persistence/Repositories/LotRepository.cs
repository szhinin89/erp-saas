using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class LotRepository : ILotRepository
{
    private readonly ErpDbContext _db;

    public LotRepository(ErpDbContext db) => _db = db;

    public Task<Lot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Lots.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<Lot?> GetByLotNumberAsync(Guid itemId, string lotNumber, CancellationToken cancellationToken = default)
        => _db.Lots.FirstOrDefaultAsync(l => l.ItemId == itemId && l.LotNumber == lotNumber, cancellationToken);

    public async Task<IReadOnlyList<Lot>> GetByItemAsync(
        Guid itemId,
        Guid? warehouseId = null,
        LotStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Lots.Where(l => l.ItemId == itemId);
        if (warehouseId.HasValue)
            query = query.Where(l => l.WarehouseId == warehouseId.Value);
        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);
        return await query.OrderBy(l => l.LotNumber).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Lot>> GetExpiringBeforeAsync(DateOnly expirationDate, CancellationToken cancellationToken = default)
        => await _db.Lots
            .Where(l => l.Status == LotStatus.Active && l.ExpirationDate.HasValue && l.ExpirationDate < expirationDate)
            .OrderBy(l => l.ExpirationDate)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid itemId, string lotNumber, CancellationToken cancellationToken = default)
        => _db.Lots.AnyAsync(l => l.ItemId == itemId && l.LotNumber == lotNumber, cancellationToken);

    public Task AddAsync(Lot lot, CancellationToken cancellationToken = default)
    {
        _db.Lots.Add(lot);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
