using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root CompanyBpPurchaseSettings — ADR-033, Fase 3.
/// Scope: ITenantScopedEntity + ICompanyScopedEntity.
/// Queries filtradas por (tenant_id, company_id) via EF Core global query filter.
/// </summary>
public interface ICompanyBpPurchaseSettingsRepository
{
    Task<CompanyBpPurchaseSettings?> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(
        CompanyBpPurchaseSettings settings,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
