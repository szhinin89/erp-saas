using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio de condiciones comerciales por empresa (CompanyBusinessPartnerSettings).
/// Filtrado implícito por SubscriberId via EF query filter.
/// </summary>
public interface ICompanyBpSettingsRepository
{
    Task<CompanyBusinessPartnerSettings?> GetAsync(
        Guid companyId, Guid businessPartnerId, CancellationToken ct = default);

    Task AddAsync(CompanyBusinessPartnerSettings settings, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
