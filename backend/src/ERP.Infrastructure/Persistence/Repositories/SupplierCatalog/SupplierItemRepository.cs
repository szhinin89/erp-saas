using ERP.Application.Common;
using ERP.Domain.Modules.SupplierCatalog.Entities;
using ERP.Domain.Modules.SupplierCatalog.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.SupplierCatalog;

public sealed class SupplierItemRepository : ISupplierItemRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentSubscriber _subscriber;

    public SupplierItemRepository(ErpDbContext context, ICurrentSubscriber subscriber)
    {
        _context    = context;
        _subscriber = subscriber;
    }

    private IQueryable<SupplierItem> Scoped()
        => _context.SupplierItems.Where(x => x.SubscriberId == _subscriber.SubscriberId);

    public async Task<SupplierItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Scoped().Include(x => x.Prices).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<SupplierItem>> GetByItemAsync(Guid itemId, CancellationToken ct = default)
        => await Scoped()
            .Include(x => x.Prices.Where(p => p.IsActive))
            .Where(x => x.ItemId == itemId && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(Guid supplierId, Guid itemId, Guid? variantId, CancellationToken ct = default)
        => await Scoped()
            .AnyAsync(x => x.SupplierId == supplierId && x.ItemId == itemId && x.VariantId == variantId, ct);

    public async Task AddAsync(SupplierItem supplierItem, CancellationToken ct = default)
        => await _context.SupplierItems.AddAsync(supplierItem, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
