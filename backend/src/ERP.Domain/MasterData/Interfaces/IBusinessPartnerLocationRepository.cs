using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root BusinessPartnerLocation.
/// Scope: ITenantScopedEntity. CompanyId eliminado — las ubicaciones son datos
/// maestros del tenant, no de la empresa. Ver ADR-BP-02 (Fase 4).
/// Queries filtradas por tenantId via EF Core global query filter.
/// </summary>
public interface IBusinessPartnerLocationRepository
{
    Task<BusinessPartnerLocation?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<BusinessPartnerLocation>> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        bool? onlyActive = true,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<BusinessPartnerLocation>> GetByPurposeAsync(
        Guid businessPartnerId,
        LocationPurpose purpose,
        CancellationToken cancellationToken = default
    );

    Task<BusinessPartnerLocation?> GetPrimaryAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Verifica si algún contacto activo referencia esta ubicación.
    /// Usado en el handler de Deactivate para enforcement del Problema 7 (Fase 4).
    /// </summary>
    Task<bool> HasActiveContactsAsync(
        Guid locationId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Quita IsPrimary de todas las ubicaciones del BP (antes de asignar nueva primaria).</summary>
    Task ClearPrimaryAsync(Guid businessPartnerId, CancellationToken cancellationToken = default);

    Task AddAsync(BusinessPartnerLocation location, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
