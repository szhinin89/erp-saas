using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface ICompanyRepository
{
    Task<CompanyEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CompanyEntity?> GetBySubscriberAndRucAsync(Guid subscriberId, string ruc, CancellationToken ct = default);

    Task<CompanyEntity?> GetByRucAsync(string ruc, CancellationToken ct = default);

    Task<IReadOnlyList<CompanyEntity>> GetActiveBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default);

    Task<IReadOnlyList<CompanyEntity>> GetByIdsForManagementAsync(
        IReadOnlyCollection<Guid> companyIds,
        Guid subscriberId,
        CancellationToken ct = default);

    Task<CompanyEntity?> GetTrackedByIdForSubscriberAsync(
        Guid companyId,
        Guid subscriberId,
        CancellationToken ct = default);

    Task<int> CountActiveBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default);

    Task<CompanyEntity?> GetByIdForSubscriberAsync(Guid companyId, Guid subscriberId, CancellationToken ct = default);

    Task<IReadOnlyList<CompanyEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken ct = default);

    Task AddAsync(CompanyEntity company, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
