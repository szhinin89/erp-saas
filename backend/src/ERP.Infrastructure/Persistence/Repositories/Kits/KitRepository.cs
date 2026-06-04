using ERP.Application.Common;
using ERP.Domain.Modules.Kits.Entities;
using ERP.Domain.Modules.Kits.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Kits;

public sealed class KitRepository : IKitRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentSubscriber _subscriber;

    public KitRepository(ErpDbContext context, ICurrentSubscriber subscriber)
    {
        _context    = context;
        _subscriber = subscriber;
    }

    private IQueryable<Kit> Scoped()
        => _context.Kits.Where(x => x.SubscriberId == _subscriber.SubscriberId);

    public async Task<Kit?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Scoped()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Kit?> GetByItemIdAsync(Guid itemId, CancellationToken ct = default)
        => await Scoped()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.ItemId == itemId, ct);

    public async Task<bool> ExistsByItemIdAsync(Guid itemId, CancellationToken ct = default)
        => await Scoped().AnyAsync(x => x.ItemId == itemId, ct);

    public async Task AddAsync(Kit kit, CancellationToken ct = default)
        => await _context.Kits.AddAsync(kit, ct);

    public async Task TrackLineAsync(KitLine line, CancellationToken ct = default)
        => await _context.KitLines.AddAsync(line, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
