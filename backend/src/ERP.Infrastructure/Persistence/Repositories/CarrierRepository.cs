using ERP.Domain.Modules.Logistics.Entities;
using ERP.Domain.Modules.Logistics.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public class CarrierRepository : ICarrierRepository
{
    private readonly ErpDbContext _context;

    public CarrierRepository(ErpDbContext context) => _context = context;

    public async Task<List<Carrier>> GetAllAsync(Guid tenantId, string? search, bool? isActive, CancellationToken ct = default)
    {
        var query = _context.Carriers
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId);

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
        _context.Carriers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsIdentificationAsync(Guid tenantId, string identificationNumber, Guid? excludeId, CancellationToken ct = default)
    {
        var query = _context.Carriers
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.IdentificationNumber == identificationNumber);

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return query.AnyAsync(ct);
    }

    public async Task AddAsync(Carrier carrier, CancellationToken ct = default) =>
        await _context.Carriers.AddAsync(carrier, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
