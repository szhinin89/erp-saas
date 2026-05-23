using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del SupplierProfile.
/// Filtrado implícito por SubscriberId via EF query filter.
/// </summary>
public interface ISupplierProfileRepository
{
    Task<SupplierProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<SupplierProfile?> GetByBusinessPartnerIdAsync(
        Guid businessPartnerId, CancellationToken ct = default);

    Task<bool> ExistsForBusinessPartnerAsync(
        Guid businessPartnerId, CancellationToken ct = default);

    Task AddAsync(SupplierProfile profile, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
