using ERP.Application.Common;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Pricing;

public sealed class PriceListItemRepository : IPriceListItemRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public PriceListItemRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<PriceListItem> Scoped(Guid tenantId)
        => _context.PriceListItems.ForOperationalScope(tenantId, _company);

    public async Task<IReadOnlyList<PriceListItem>> GetByItemAsync(Guid tenantId, Guid itemId, CancellationToken ct = default)
        => await Scoped(tenantId).Where(x => x.ItemId == itemId).ToListAsync(ct);

    public async Task<IReadOnlyList<PriceListItem>> GetByPriceListAsync(Guid tenantId, Guid priceListId, CancellationToken ct = default)
        => await Scoped(tenantId).Where(x => x.PriceListId == priceListId && x.IsActive).ToListAsync(ct);

    public Task<PriceListItem?> FindByKeyAsync(Guid tenantId, Guid priceListId, Guid itemId, CancellationToken ct = default)
        => Scoped(tenantId).FirstOrDefaultAsync(x => x.PriceListId == priceListId && x.ItemId == itemId, ct);

    public Task AddAsync(PriceListItem assignment, CancellationToken ct = default)
        => _context.PriceListItems.AddAsync(assignment, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
