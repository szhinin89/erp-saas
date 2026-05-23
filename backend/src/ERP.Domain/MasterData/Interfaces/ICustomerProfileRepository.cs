using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del CustomerProfile.
/// Filtrado implícito por SubscriberId via EF query filter.
/// </summary>
public interface ICustomerProfileRepository
{
    Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CustomerProfile?> GetByBusinessPartnerIdAsync(
        Guid businessPartnerId, CancellationToken ct = default);

    Task<bool> ExistsForBusinessPartnerAsync(
        Guid businessPartnerId, CancellationToken ct = default);

    Task AddAsync(CustomerProfile profile, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
