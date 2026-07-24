using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root BusinessPartner.
/// Todas las queries están implícitamente filtradas por tenantId
/// via EF Core global query filter. No pasar tenantId explícito.
/// </summary>
public interface IBusinessPartnerRepository
{
    Task<BusinessPartner?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch name lookup — single DB query for N partner IDs.
    /// Missing IDs (deleted or cross-tenant) are silently omitted from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<BusinessPartner?> GetByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Existence-only check por identificación (tipo + número), tenant-scoped via
    /// query filter. No materializa la entidad completa. Usar excludeId al validar
    /// una actualización sobre el mismo BusinessPartner.
    /// </summary>
    Task<bool> ExistsByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

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
        CancellationToken cancellationToken  = default);

    Task<int> CountAsync(
        string?     query    = null,
        bool?       isActive = true,
        RoleType[]? roles    = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(BusinessPartner businessPartner, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
