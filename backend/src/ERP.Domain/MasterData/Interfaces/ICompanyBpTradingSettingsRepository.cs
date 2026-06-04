using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del aggregate root CompanyBpTradingSettings.
/// Scope: ISubscriberScopedEntity + ICompanyScopedEntity.
/// Queries filtradas por (subscriber_id, company_id) via EF Core global query filter.
/// </summary>
public interface ICompanyBpTradingSettingsRepository
{
    Task<CompanyBpTradingSettings?> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken ct = default);

    Task<IReadOnlyList<CompanyBpTradingSettings>> GetBlockedAsync(
        CancellationToken ct = default);

    Task AddAsync(CompanyBpTradingSettings settings, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
