using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del Aggregate Root SupplierRetentionDefault —
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01.
/// Scope: ITenantScopedEntity + ICompanyScopedEntity.
/// Queries filtradas por (tenant_id, company_id) vía EF Core global query filter.
/// </summary>
public interface ISupplierRetentionDefaultRepository
{
    /// <summary>Todas las filas (activas e inactivas) de un proveedor en la empresa activa, ordenadas por DisplayOrder.</summary>
    Task<IReadOnlyList<SupplierRetentionDefault>> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Solo las filas activas de un proveedor en la empresa activa — usado por elegibilidad/cálculo de retención.</summary>
    Task<IReadOnlyList<SupplierRetentionDefault>> GetActiveByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    );

    Task<SupplierRetentionDefault?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(
        SupplierRetentionDefault entry,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
