using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root BusinessPartnerRole.
/// Queries filtradas por tenantId via EF Core global query filter.
///
/// UPSERT SEMÁNTICO (ADR-BP-12):
///   - GetByTypeAsync: busca rol existente (activo o revocado) para decidir Create vs Reactivate.
///   - HasActiveRoleAsync: validación rápida de si el BP tiene un rol activo.
/// </summary>
public interface IBusinessPartnerRoleRepository
{
    Task<BusinessPartnerRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Busca un rol por tipo para un BP específico (activo o revocado).</summary>
    Task<BusinessPartnerRole?> GetByTypeAsync(
        Guid businessPartnerId,
        RoleType roleType,
        CancellationToken cancellationToken = default
    );

    /// <summary>Todos los roles de un BP (activos + revocados).</summary>
    Task<IReadOnlyList<BusinessPartnerRole>> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        bool? onlyActive = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>Verifica si el BP tiene un rol activo de un tipo dado.</summary>
    Task<bool> HasActiveRoleAsync(
        Guid businessPartnerId,
        RoleType roleType,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(BusinessPartnerRole role, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
