using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.Pricing;

public sealed class PriceListRepository : IPriceListRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public PriceListRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<PriceList> Scoped(Guid tenantId)
        => _context.PriceLists.ForOperationalScope(tenantId, _company);

    public Task<PriceList?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Scoped(tenantId).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<PriceList>> GetAllAsync(
        Guid tenantId, bool? activeFilter, string? search, CancellationToken ct = default)
    {
        var q = Scoped(tenantId);

        if (activeFilter is true)       q = q.Where(p => p.IsActive);
        else if (activeFilter is false) q = q.Where(p => !p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p => p.Name.Contains(s) || p.Code.Contains(s));
        }

        return await q.OrderBy(p => p.Code).ToListAsync(ct);
    }

    public async Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludeId, CancellationToken ct = default)
    {
        var q = Scoped(tenantId).Where(p => p.Code == code.Trim().ToUpperInvariant());
        if (excludeId.HasValue) q = q.Where(p => p.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    public async Task<bool> DefaultExistsAsync(Guid tenantId, Guid? excludeId, CancellationToken ct = default)
    {
        var q = Scoped(tenantId).Where(p => p.IsDefault && p.IsActive);
        if (excludeId.HasValue) q = q.Where(p => p.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    public Task AddAsync(PriceList priceList, CancellationToken ct = default)
        => _context.PriceLists.AddAsync(priceList, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
