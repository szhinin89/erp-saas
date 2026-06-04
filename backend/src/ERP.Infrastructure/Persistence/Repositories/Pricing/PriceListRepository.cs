using ERP.Application.Common;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Pricing;

public sealed class PriceListRepository : IPriceListRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentCompany _company;

    public PriceListRepository(ErpDbContext context, ICurrentSubscriber subscriber, ICurrentCompany company)
    {
        _context    = context;
        _subscriber = subscriber;
        _company    = company;
    }

    // PriceList es ICompanyOperationalEntity — filtra por subscriber + company.
    private IQueryable<PriceList> Scoped()
        => _context.PriceLists
            .Where(x => x.SubscriberId == _subscriber.SubscriberId)
            .Where(x => x.CompanyId == _company.CompanyId);

    public async Task<PriceList?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Scoped()
            .Include(x => x.Entries)
            .Include(x => x.Discounts)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PriceList?> GetDefaultAsync(CancellationToken ct = default)
        => await Scoped()
            .Include(x => x.Entries)
            .Include(x => x.Discounts)
            .FirstOrDefaultAsync(x => x.IsDefault && x.IsActive, ct);

    public async Task<IReadOnlyList<PriceList>> GetAllByCompanyAsync(CancellationToken ct = default)
        => await Scoped()
            .OrderBy(x => x.Code)
            .ToListAsync(ct);

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await Scoped().AnyAsync(x => x.Code == normalized, ct);
    }

    public async Task<bool> HasDefaultAsync(CancellationToken ct = default)
        => await Scoped().AnyAsync(x => x.IsDefault && x.IsActive, ct);

    public async Task<IReadOnlyList<PriceListEntry>> GetEntriesByItemAsync(
        Guid itemId, Guid? variantId, CancellationToken ct = default)
        => await _context.PriceListEntries
            .Where(e => e.SubscriberId == _subscriber.SubscriberId)
            .Where(e => e.ItemId == itemId)
            .Where(e => variantId == null || e.VariantId == variantId)
            .Where(e => e.IsActive)
            .Join(_context.PriceLists.Where(pl =>
                    pl.SubscriberId == _subscriber.SubscriberId &&
                    pl.CompanyId == _company.CompanyId &&
                    pl.IsActive),
                entry => entry.PriceListId,
                pl => pl.Id,
                (entry, _) => entry)
            .OrderBy(e => e.MinQty)
            .ToListAsync(ct);

    public async Task AddAsync(PriceList priceList, CancellationToken ct = default)
        => await _context.PriceLists.AddAsync(priceList, ct);

    public async Task TrackEntryAsync(PriceListEntry entry, CancellationToken ct = default)
        => await _context.PriceListEntries.AddAsync(entry, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
