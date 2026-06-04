using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root BusinessPartnerContact.
/// Scope: ISubscriberScopedEntity. CompanyId eliminado — los contactos son datos
/// maestros del tenant, no de la empresa. Ver ADR-BP-02 (Fase 4).
/// Queries filtradas por SubscriberId via EF Core global query filter.
/// </summary>
public interface IBusinessPartnerContactRepository
{
    Task<BusinessPartnerContact?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<BusinessPartnerContact>> GetByBusinessPartnerAsync(
        Guid  businessPartnerId,
        bool? onlyActive = true,
        CancellationToken ct = default);

    Task<IReadOnlyList<BusinessPartnerContact>> GetByRoleAsync(
        Guid        businessPartnerId,
        ContactRole role,
        CancellationToken ct = default);

    Task<BusinessPartnerContact?> GetPrimaryAsync(
        Guid businessPartnerId,
        CancellationToken ct = default);

    /// <summary>Quita IsPrimary de todos los contactos del BP (antes de asignar nuevo primario).</summary>
    Task ClearPrimaryAsync(Guid businessPartnerId, CancellationToken ct = default);

    Task AddAsync(BusinessPartnerContact contact, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
