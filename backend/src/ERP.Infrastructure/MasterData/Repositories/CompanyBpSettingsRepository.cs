using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

public sealed class CompanyBpSettingsRepository : ICompanyBpSettingsRepository
{
    private readonly ErpDbContext _db;

    public CompanyBpSettingsRepository(ErpDbContext db) => _db = db;

    public Task<CompanyBusinessPartnerSettings?> GetAsync(
        Guid companyId, Guid businessPartnerId, CancellationToken ct = default)
        => _db.CompanyBusinessPartnerSettings
              .FirstOrDefaultAsync(x =>
                  x.CompanyId         == companyId &&
                  x.BusinessPartnerId == businessPartnerId, ct);

    public async Task AddAsync(CompanyBusinessPartnerSettings settings, CancellationToken ct = default)
        => await _db.CompanyBusinessPartnerSettings.AddAsync(settings, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
