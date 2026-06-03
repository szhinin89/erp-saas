using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

public interface IBusinessPartnerLocationRepository
{
    Task<BusinessPartnerLocation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<BusinessPartnerLocation>> GetByBusinessPartnerAsync(
        Guid subscriberId, Guid companyId, Guid businessPartnerId,
        bool? onlyActive = true, CancellationToken ct = default);

    Task<BusinessPartnerLocation?> GetPrimaryAsync(
        Guid subscriberId, Guid companyId, Guid businessPartnerId,
        CancellationToken ct = default);

    /// <summary>Quita el flag IsPrimary de TODAS las ubicaciones del BP en la company (antes de asignar nueva primaria).</summary>
    Task ClearPrimaryAsync(Guid subscriberId, Guid companyId, Guid businessPartnerId, CancellationToken ct = default);

    Task AddAsync(BusinessPartnerLocation location, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
