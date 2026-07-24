using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface ICompanyRepository
{
    Task<CompanyEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CompanyEntity?> GetTrackedByIdForIntegrationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CompanyEntity?> GetByTenantAndTaxIdentificationNumberAsync(Guid tenantId, string taxIdentificationNumber, CancellationToken cancellationToken = default);

    Task<CompanyEntity?> GetByTaxIdentificationNumberAsync(string taxIdentificationNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyEntity>> GetActiveByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyEntity>> GetByIdsForManagementAsync(
        IReadOnlyCollection<Guid> companyIds,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<CompanyEntity?> GetTrackedByIdForTenantAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<CompanyEntity?> GetByIdForTenantAsync(Guid companyId, Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default);

    Task AddAsync(CompanyEntity company, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
