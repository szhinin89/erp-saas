using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root CompanyBpTradingSettings.
/// Scope: ITenantScopedEntity + ICompanyScopedEntity.
/// Queries filtradas por (tenant_id, company_id) via EF Core global query filter.
/// </summary>
public interface ICompanyBpTradingSettingsRepository
{
    Task<CompanyBpTradingSettings?> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<CompanyBpTradingSettings>> GetBlockedAsync(
        CancellationToken cancellationToken = default
    );

    Task AddAsync(CompanyBpTradingSettings settings, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
