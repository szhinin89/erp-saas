using ERP.Domain.Modules.Company.Entities;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface IEstablishmentRepository
{
    Task<Establishment?> GetMainByCompanyAsync(Guid subscriberId, Guid companyId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid subscriberId, Guid companyId, string code, CancellationToken ct = default);
    Task AddAsync(Establishment establishment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
