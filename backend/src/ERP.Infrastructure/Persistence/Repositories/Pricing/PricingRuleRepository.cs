using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.Pricing;

public sealed class PricingRuleRepository : IPricingRuleRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public PricingRuleRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<PricingRule> Scoped(Guid tenantId)
        => _context.PricingRules.ForOperationalScope(tenantId, _company);

    public Task<PricingRule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Scoped(tenantId).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<PricingRule>> GetByPriceListAsync(
        Guid tenantId, Guid priceListId, CancellationToken ct = default)
        => await Scoped(tenantId)
            .Where(p => p.PriceListId == priceListId && p.IsActive)
            .OrderBy(p => p.ItemId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PricingRule>> GetByItemAsync(
        Guid tenantId, Guid itemId, CancellationToken ct = default)
        => await Scoped(tenantId)
            .Where(p => p.ItemId == itemId && p.IsActive)
            .OrderBy(p => p.PriceListId)
            .ToListAsync(ct);

    public Task<PricingRule?> GetActiveForItemInListAsync(
        Guid tenantId, Guid priceListId, Guid itemId, CancellationToken ct = default)
        => Scoped(tenantId)
            .FirstOrDefaultAsync(p => p.PriceListId == priceListId && p.ItemId == itemId && p.IsActive, ct);

    public Task<PricingRule?> FindByKeyAsync(
        Guid tenantId, Guid priceListId, Guid itemId, CancellationToken ct = default)
        => Scoped(tenantId)
            .FirstOrDefaultAsync(p => p.PriceListId == priceListId && p.ItemId == itemId, ct);

    public Task AddAsync(PricingRule rule, CancellationToken ct = default)
        => _context.PricingRules.AddAsync(rule, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
