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

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Company?> GetTrackedByIdForIntegrationAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Company?> GetByTenantAndTaxIdentificationNumberAsync(Guid tenantId, string taxIdentificationNumber, CancellationToken cancellationToken = default)
        => _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.TaxIdentificationNumber == taxIdentificationNumber, cancellationToken);

    // El número de identificación tributaria (RUC) es único globalmente entre todos los tenants (ley tributaria EC).
    public Task<Company?> GetByTaxIdentificationNumberAsync(string taxIdentificationNumber, CancellationToken cancellationToken = default)
        => _db.Companies.IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TaxIdentificationNumber == taxIdentificationNumber, cancellationToken);

    public async Task<IReadOnlyList<Company>> GetByIdsForManagementAsync(
        IReadOnlyCollection<Guid> companyIds,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (companyIds.Count == 0)
            return Array.Empty<Company>();
        return await _db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id) && c.TenantId == tenantId)
            .OrderBy(c => c.LegalName)
            .ToListAsync(cancellationToken);
    }

    public Task<Company?> GetTrackedByIdForTenantAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
        => _db.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<Company>> GetActiveByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await _db.Companies.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderBy(c => c.LegalName)
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _db.Companies.CountAsync(c => c.TenantId == tenantId && c.IsActive, cancellationToken);

    public Task<Company?> GetByIdForTenantAsync(Guid companyId, Guid tenantId, CancellationToken cancellationToken = default)
        => _db.Companies.IgnoreQueryFilters()
            .Include(c => c.TaxRegime)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.TenantId == tenantId && c.IsActive, cancellationToken);

    public async Task<IReadOnlyList<Company>> GetByIdsAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default)
    {
        if (companyIds.Count == 0)
            return Array.Empty<Company>();

        return await _db.Companies.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => companyIds.Contains(c.Id) && c.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
        => await _db.Companies.AddAsync(company, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
