using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Inventory;

public sealed class LotRepository : ILotRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentCompany _company;

    public LotRepository(ErpDbContext context, ICurrentSubscriber subscriber, ICurrentCompany company)
    {
        _context    = context;
        _subscriber = subscriber;
        _company    = company;
    }

    private IQueryable<Lot> Scoped()
        => _context.Lots
            .Where(x => x.SubscriberId == _subscriber.SubscriberId)
            .Where(x => x.CompanyId == _company.CompanyId);

    public async Task<Lot?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Scoped().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Lot?> GetByLotNumberAsync(
        Guid itemId, string lotNumber, CancellationToken ct = default)
        => await Scoped()
            .FirstOrDefaultAsync(x =>
                x.ItemId == itemId &&
                x.LotNumber == lotNumber, ct);

    public async Task<IReadOnlyList<Lot>> GetByItemAsync(
        Guid itemId,
        Guid? warehouseId = null,
        LotStatus? status = null,
        CancellationToken ct = default)
    {
        var query = Scoped().Where(x => x.ItemId == itemId);

        if (warehouseId.HasValue)
            query = query.Where(x => x.WarehouseId == warehouseId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Lot>> GetExpiringBeforeAsync(
        DateOnly date, CancellationToken ct = default)
        => await Scoped()
            .Where(x =>
                x.Status == LotStatus.Active &&
                x.ExpirationDate.HasValue &&
                x.ExpirationDate.Value <= date)
            .OrderBy(x => x.ExpirationDate)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(
        Guid itemId, string lotNumber, CancellationToken ct = default)
        => await Scoped()
            .AnyAsync(x => x.ItemId == itemId && x.LotNumber == lotNumber, ct);

    public async Task AddAsync(Lot lot, CancellationToken ct = default)
        => await _context.Lots.AddAsync(lot, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
