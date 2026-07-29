using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root BusinessPartnerContact.
/// Scope: ITenantScopedEntity. CompanyId eliminado — los contactos son datos
/// maestros del tenant, no de la empresa. Ver ADR-BP-02 (Fase 4).
/// Queries filtradas por tenantId via EF Core global query filter.
/// </summary>
public interface IBusinessPartnerContactRepository
{
    Task<BusinessPartnerContact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BusinessPartnerContact>> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        bool? onlyActive = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BusinessPartnerContact>> GetByRoleAsync(
        Guid businessPartnerId,
        ContactRole role,
        CancellationToken cancellationToken = default);

    Task<BusinessPartnerContact?> GetPrimaryAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default);

    /// <summary>Quita IsPrimary de todos los contactos del BP (antes de asignar nuevo primario).</summary>
    Task ClearPrimaryAsync(Guid businessPartnerId, CancellationToken cancellationToken = default);

    Task AddAsync(BusinessPartnerContact contact, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
