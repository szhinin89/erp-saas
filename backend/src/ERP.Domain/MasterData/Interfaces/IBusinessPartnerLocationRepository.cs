using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root BusinessPartnerLocation.
/// Scope: ISubscriberScopedEntity. CompanyId eliminado — las ubicaciones son datos
/// maestros del tenant, no de la empresa. Ver ADR-BP-02 (Fase 4).
/// Queries filtradas por SubscriberId via EF Core global query filter.
/// </summary>
public interface IBusinessPartnerLocationRepository
{
    Task<BusinessPartnerLocation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<BusinessPartnerLocation>> GetByBusinessPartnerAsync(
        Guid  businessPartnerId,
        bool? onlyActive = true,
        CancellationToken ct = default);

    Task<IReadOnlyList<BusinessPartnerLocation>> GetByPurposeAsync(
        Guid            businessPartnerId,
        LocationPurpose purpose,
        CancellationToken ct = default);

    Task<BusinessPartnerLocation?> GetPrimaryAsync(
        Guid businessPartnerId,
        CancellationToken ct = default);

    /// <summary>
    /// Verifica si algún contacto activo referencia esta ubicación.
    /// Usado en el handler de Deactivate para enforcement del Problema 7 (Fase 4).
    /// </summary>
    Task<bool> HasActiveContactsAsync(Guid locationId, CancellationToken ct = default);

    /// <summary>Quita IsPrimary de todas las ubicaciones del BP (antes de asignar nueva primaria).</summary>
    Task ClearPrimaryAsync(Guid businessPartnerId, CancellationToken ct = default);

    Task AddAsync(BusinessPartnerLocation location, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
