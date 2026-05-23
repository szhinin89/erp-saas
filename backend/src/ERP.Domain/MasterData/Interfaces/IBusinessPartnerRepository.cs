using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root BusinessPartner.
///
/// Todas las queries están implícitamente filtradas por SubscriberId
/// via EF Core query filter (ISubscriberScopedEntity).
/// No pasar subscriberId explícito en los métodos — viene del contexto de sesión.
/// </summary>
public interface IBusinessPartnerRepository
{
    /// <summary>Busca por Id dentro del subscriber activo.</summary>
    Task<BusinessPartner?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Busca por tipo + número de identificación (unicidad por subscriber).
    /// Usar para detectar duplicados antes de crear.
    /// </summary>
    Task<BusinessPartner?> GetByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        CancellationToken ct = default);

    /// <summary>Lista con filtros opcionales y paginación.</summary>
    Task<IReadOnlyList<BusinessPartner>> SearchAsync(
        string?  query      = null,
        bool?    isActive   = true,
        bool?    isCustomer = null,
        bool?    isSupplier = null,
        int      skip       = 0,
        int      take       = 50,
        CancellationToken ct = default);

    /// <summary>Total de registros que coinciden con los filtros (sin Skip/Take).</summary>
    Task<int> CountAsync(
        string?  query      = null,
        bool?    isActive   = true,
        bool?    isCustomer = null,
        bool?    isSupplier = null,
        CancellationToken ct = default);

    /// <summary>Verdadero si ya existe un BP con esa identificación en el subscriber activo.</summary>
    Task<bool> ExistsByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        Guid?  excludeId = null,
        CancellationToken ct = default);

    Task AddAsync(BusinessPartner businessPartner, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
