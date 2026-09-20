using ERP.Application.Common;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Pricing;

public sealed class PriceListCustomerRepository : IPriceListCustomerRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public PriceListCustomerRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<PriceListCustomer> Scoped(Guid tenantId) =>
        _context.PriceListCustomers.ForOperationalScope(tenantId, _company);

    public async Task<IReadOnlyList<PriceListCustomer>> GetByCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken ct = default
    ) => await Scoped(tenantId).Where(x => x.CustomerId == customerId).ToListAsync(ct);

    public async Task<IReadOnlyList<PriceListCustomer>> GetByPriceListAsync(
        Guid tenantId,
        Guid priceListId,
        CancellationToken ct = default
    ) =>
        await Scoped(tenantId)
            .Where(x => x.PriceListId == priceListId && x.IsActive)
            .ToListAsync(ct);

    public Task<PriceListCustomer?> FindByKeyAsync(
        Guid tenantId,
        Guid priceListId,
        Guid customerId,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .FirstOrDefaultAsync(x => x.PriceListId == priceListId && x.CustomerId == customerId, ct);

    public Task AddAsync(PriceListCustomer assignment, CancellationToken ct = default) =>
        _context.PriceListCustomers.AddAsync(assignment, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
