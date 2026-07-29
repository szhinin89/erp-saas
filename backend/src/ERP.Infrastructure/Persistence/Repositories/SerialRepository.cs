using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SerialRepository : ISerialRepository
{
    private readonly ErpDbContext _db;

    public SerialRepository(ErpDbContext db) => _db = db;

    public Task<SerialNumber?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => _db.SerialNumbers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<SerialNumber?> GetBySerialAsync(
        Guid itemId,
        string serial,
        CancellationToken cancellationToken = default
    ) =>
        _db.SerialNumbers.FirstOrDefaultAsync(
            s => s.ItemId == itemId && s.Serial == serial,
            cancellationToken
        );

    public async Task<IReadOnlyList<SerialNumber>> GetByItemAsync(
        Guid itemId,
        Guid? warehouseId = null,
        SerialStatus? status = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = _db.SerialNumbers.Where(s => s.ItemId == itemId);
        if (warehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == warehouseId.Value);
        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);
        return await query.OrderBy(s => s.Serial).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SerialNumber>> GetByLotAsync(
        Guid lotId,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SerialNumbers.Where(s => s.LotId == lotId)
            .OrderBy(s => s.Serial)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(
        Guid itemId,
        string serial,
        CancellationToken cancellationToken = default
    ) =>
        _db.SerialNumbers.AnyAsync(
            s => s.ItemId == itemId && s.Serial == serial,
            cancellationToken
        );

    public Task AddAsync(SerialNumber serial, CancellationToken cancellationToken = default)
    {
        _db.SerialNumbers.Add(serial);
        return Task.CompletedTask;
    }

    public async Task AddRangeAsync(
        IEnumerable<SerialNumber> serials,
        CancellationToken cancellationToken = default
    )
    {
        await _db.SerialNumbers.AddRangeAsync(serials, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
