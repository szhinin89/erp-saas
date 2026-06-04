using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root BusinessPartner.
/// Todas las queries están implícitamente filtradas por SubscriberId
/// via EF Core global query filter. No pasar subscriberId explícito.
/// </summary>
public interface IBusinessPartnerRepository
{
    Task<BusinessPartner?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<BusinessPartner?> GetByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Búsqueda con filtros. roles: lista de RoleType requeridos (JOIN a master_bp_roles).
    /// Reemplaza los obsoletos isCustomer/isSupplier booleans.
    /// </summary>
    Task<IReadOnlyList<BusinessPartner>> SearchAsync(
        string?      query    = null,
        bool?        isActive = true,
        RoleType[]?  roles    = null,
        int          skip     = 0,
        int          take     = 50,
        CancellationToken ct  = default);

    Task<int> CountAsync(
        string?     query    = null,
        bool?       isActive = true,
        RoleType[]? roles    = null,
        CancellationToken ct = default);

    Task AddAsync(BusinessPartner businessPartner, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
