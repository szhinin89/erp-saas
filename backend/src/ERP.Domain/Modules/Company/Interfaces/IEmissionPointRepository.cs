using ERP.Domain.Modules.Company.Entities;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface IEmissionPointRepository
{
    Task<IReadOnlyList<EmissionPoint>> GetByEstablishmentAsync(Guid subscriberId, Guid establishmentId, CancellationToken ct = default);
    Task<EmissionPoint?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default);
    Task<EmissionPoint?> GetDefaultForBranchAsync(Guid subscriberId, Guid branchId, CancellationToken ct = default);
    Task<EmissionPoint?> GetDefaultForCompanyAsync(Guid subscriberId, Guid companyId, CancellationToken ct = default);
    Task ClearDefaultExceptAsync(Guid subscriberId, Guid establishmentId, Guid? exceptId, Guid updatedBy, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid subscriberId, Guid establishmentId, string code, CancellationToken ct = default);
    Task AddAsync(EmissionPoint emissionPoint, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
