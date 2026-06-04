using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Inventory;

public sealed class SerialRepository : ISerialRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentCompany _company;

    public SerialRepository(ErpDbContext context, ICurrentSubscriber subscriber, ICurrentCompany company)
    {
        _context    = context;
        _subscriber = subscriber;
        _company    = company;
    }

    private IQueryable<SerialNumber> Scoped()
        => _context.SerialNumbers
            .Where(x => x.SubscriberId == _subscriber.SubscriberId)
            .Where(x => x.CompanyId == _company.CompanyId);

    public async Task<SerialNumber?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Scoped().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<SerialNumber?> GetBySerialAsync(
        Guid itemId, string serial, CancellationToken ct = default)
        => await Scoped()
            .FirstOrDefaultAsync(x =>
                x.ItemId == itemId &&
                x.Serial == serial, ct);

    public async Task<IReadOnlyList<SerialNumber>> GetByItemAsync(
        Guid itemId,
        Guid? warehouseId = null,
        SerialStatus? status = null,
        CancellationToken ct = default)
    {
        var query = Scoped().Where(x => x.ItemId == itemId);

        if (warehouseId.HasValue)
            query = query.Where(x => x.WarehouseId == warehouseId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await query
            .OrderBy(x => x.Serial)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SerialNumber>> GetByLotAsync(
        Guid lotId, CancellationToken ct = default)
        => await Scoped()
            .Where(x => x.LotId == lotId)
            .OrderBy(x => x.Serial)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(
        Guid itemId, string serial, CancellationToken ct = default)
        => await Scoped()
            .AnyAsync(x => x.ItemId == itemId && x.Serial == serial, ct);

    public async Task AddAsync(SerialNumber serial, CancellationToken ct = default)
        => await _context.SerialNumbers.AddAsync(serial, ct);

    public async Task AddRangeAsync(IEnumerable<SerialNumber> serials, CancellationToken ct = default)
        => await _context.SerialNumbers.AddRangeAsync(serials, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
