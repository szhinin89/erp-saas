using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

public sealed class CompanyBpSalesSettingsRepository : ICompanyBpSalesSettingsRepository
{
    private readonly ErpDbContext _db;

    public CompanyBpSalesSettingsRepository(ErpDbContext db) => _db = db;

                    public Task<CompanyBpSalesSettings?> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    ) =>
        _db.CompanyBpSalesSettings.FirstOrDefaultAsync(
            s => s.BusinessPartnerId == businessPartnerId,
            cancellationToken
        );

    public async Task AddAsync(
        CompanyBpSalesSettings settings,
        CancellationToken cancellationToken = default
    ) => await _db.CompanyBpSalesSettings.AddAsync(settings, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
