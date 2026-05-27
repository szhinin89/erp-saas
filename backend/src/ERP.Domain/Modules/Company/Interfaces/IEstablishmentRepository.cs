using ERP.Domain.Modules.Company.Entities;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface IEstablishmentRepository
{
    Task<Establishment?> GetMainByBranchAsync(Guid subscriberId, Guid branchId, CancellationToken ct = default);
    Task<Establishment?> GetMainByCompanyAsync(Guid subscriberId, Guid companyId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid subscriberId, Guid branchId, string code, CancellationToken ct = default);
    Task AddAsync(Establishment establishment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
