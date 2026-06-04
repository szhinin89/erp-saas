using ERP.Application.Common;
using ERP.Domain.Modules.DigitalItems.Entities;
using ERP.Domain.Modules.DigitalItems.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.DigitalItems;

public sealed class DigitalItemRepository : IDigitalItemRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentSubscriber _subscriber;

    public DigitalItemRepository(ErpDbContext context, ICurrentSubscriber subscriber)
    {
        _context    = context;
        _subscriber = subscriber;
    }

    private IQueryable<DigitalItem> Scoped()
        => _context.DigitalItems.Where(x => x.SubscriberId == _subscriber.SubscriberId);

    public async Task<DigitalItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Scoped().Include(x => x.Deliverables).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<DigitalItem?> GetByItemIdAsync(Guid itemId, CancellationToken ct = default)
        => await Scoped().Include(x => x.Deliverables).FirstOrDefaultAsync(x => x.ItemId == itemId, ct);

    public async Task<bool> ExistsByItemIdAsync(Guid itemId, CancellationToken ct = default)
        => await Scoped().AnyAsync(x => x.ItemId == itemId, ct);

    public async Task AddAsync(DigitalItem item, CancellationToken ct = default)
        => await _context.DigitalItems.AddAsync(item, ct);

    public async Task TrackDeliverableAsync(DigitalDeliverable deliverable, CancellationToken ct = default)
        => await _context.DigitalDeliverables.AddAsync(deliverable, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
