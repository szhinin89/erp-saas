using Microsoft.EntityFrameworkCore;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public class TaxRateRepository : ITaxRateRepository
{
    private readonly ErpDbContext _context;

    public TaxRateRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<TaxRate?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
        => await _context.TaxRates.FirstOrDefaultAsync(t => t.Id == id && t.SubscriberId == subscriberId, ct);
}

