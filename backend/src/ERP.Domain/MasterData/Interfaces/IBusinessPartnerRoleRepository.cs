using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root BusinessPartnerRole.
/// Queries filtradas por SubscriberId via EF Core global query filter.
///
/// UPSERT SEMÁNTICO (ADR-BP-12):
///   - GetByTypeAsync: busca rol existente (activo o revocado) para decidir Create vs Reactivate.
///   - HasActiveRoleAsync: validación rápida de si el BP tiene un rol activo.
/// </summary>
public interface IBusinessPartnerRoleRepository
{
    Task<BusinessPartnerRole?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Busca un rol por tipo para un BP específico (activo o revocado).</summary>
    Task<BusinessPartnerRole?> GetByTypeAsync(
        Guid      businessPartnerId,
        RoleType  roleType,
        CancellationToken ct = default);

    /// <summary>Todos los roles de un BP (activos + revocados).</summary>
    Task<IReadOnlyList<BusinessPartnerRole>> GetByBusinessPartnerAsync(
        Guid  businessPartnerId,
        bool? onlyActive = true,
        CancellationToken ct = default);

    /// <summary>Verifica si el BP tiene un rol activo de un tipo dado.</summary>
    Task<bool> HasActiveRoleAsync(
        Guid     businessPartnerId,
        RoleType roleType,
        CancellationToken ct = default);

    Task AddAsync(BusinessPartnerRole role, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
