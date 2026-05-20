using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly ErpDbContext _db;

    public CompanyRepository(ErpDbContext db)
    {
        _db = db;
    }

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Company?> GetBySubscriberAndRucAsync(Guid subscriberId, string ruc, CancellationToken ct = default)
        => _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.SubscriberId == subscriberId && c.Ruc == ruc, ct);

    public Task<Company?> GetByRucAsync(string ruc, CancellationToken ct = default)
        => _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Ruc == ruc, ct);

    public async Task<IReadOnlyList<Company>> GetByIdsForManagementAsync(
        IReadOnlyCollection<Guid> companyIds,
        Guid subscriberId,
        CancellationToken ct = default)
    {
        if (companyIds.Count == 0)
            return Array.Empty<Company>();
        return await _db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id) && c.SubscriberId == subscriberId)
            .OrderBy(c => c.LegalName)
            .ToListAsync(ct);
    }

    public Task<Company?> GetTrackedByIdForSubscriberAsync(
        Guid companyId,
        Guid subscriberId,
        CancellationToken ct = default)
        => _db.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.SubscriberId == subscriberId, ct);

    public async Task<IReadOnlyList<Company>> GetActiveBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default)
        => await _db.Companies.AsNoTracking()
            .Where(c => c.SubscriberId == subscriberId && c.IsActive)
            .OrderBy(c => c.LegalName)
            .ToListAsync(ct);

    public Task<int> CountActiveBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default)
        => _db.Companies.CountAsync(c => c.SubscriberId == subscriberId && c.IsActive, ct);

    public Task<Company?> GetByIdForSubscriberAsync(Guid companyId, Guid subscriberId, CancellationToken ct = default)
        => _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.SubscriberId == subscriberId && c.IsActive, ct);

    public async Task<IReadOnlyList<Company>> GetByIdsAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken ct = default)
    {
        if (companyIds.Count == 0)
            return Array.Empty<Company>();
        return await _db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id) && c.IsActive)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Company company, CancellationToken ct = default)
        => await _db.Companies.AddAsync(company, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
