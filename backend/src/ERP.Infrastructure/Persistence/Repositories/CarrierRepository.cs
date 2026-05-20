using ERP.Application.Common;
using ERP.Domain.Modules.Logistics.Entities;
using ERP.Domain.Modules.Logistics.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public class CarrierRepository : ICarrierRepository
{
    private readonly ErpDbContext _context;
    private readonly IPlatformQueryAccessor _platform;

    public CarrierRepository(ErpDbContext context, IPlatformQueryAccessor platform)
    {
        _context = context;
        _platform = platform;
    }

    public async Task<List<Carrier>> GetAllAsync(Guid subscriberId, string? search, bool? isActive, CancellationToken ct = default)
    {
        var query = _platform.Unfiltered(_context.Carriers, PlatformQueryReason.TenantScopedExplicit)
            .Where(c => c.SubscriberId == subscriberId);

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.LegalName.ToLower().Contains(term)           ||
                c.IdentificationNumber.ToLower().Contains(term) ||
                c.LicensePlate.ToLower().Contains(term));
        }

        return await query.OrderBy(c => c.LegalName).ToListAsync(ct);
    }

    public Task<Carrier?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _platform.Unfiltered(_context.Carriers, PlatformQueryReason.TenantScopedExplicit)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsIdentificationAsync(Guid subscriberId, string identificationNumber, Guid? excludeId, CancellationToken ct = default)
    {
        var query = _platform.Unfiltered(_context.Carriers, PlatformQueryReason.TenantScopedExplicit)
            .Where(c => c.SubscriberId == subscriberId && c.IdentificationNumber == identificationNumber);

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return query.AnyAsync(ct);
    }

    public async Task AddAsync(Carrier carrier, CancellationToken ct = default) =>
        await _context.Carriers.AddAsync(carrier, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
