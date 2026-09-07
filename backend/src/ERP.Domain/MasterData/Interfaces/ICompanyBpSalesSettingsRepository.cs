using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

public interface ICompanyBpSalesSettingsRepository
{
    Task<CompanyBpSalesSettings?> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(
        CompanyBpSalesSettings settings,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
