using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

public interface IBusinessPartnerContactRepository
{
    Task<BusinessPartnerContact?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<BusinessPartnerContact>> GetByBusinessPartnerAsync(
        Guid subscriberId, Guid companyId, Guid businessPartnerId,
        bool? onlyActive = true, CancellationToken ct = default);

    Task<BusinessPartnerContact?> GetPrimaryAsync(
        Guid subscriberId, Guid companyId, Guid businessPartnerId,
        CancellationToken ct = default);

    Task ClearPrimaryAsync(Guid subscriberId, Guid companyId, Guid businessPartnerId, CancellationToken ct = default);

    Task AddAsync(BusinessPartnerContact contact, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
