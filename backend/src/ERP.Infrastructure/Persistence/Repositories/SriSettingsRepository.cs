using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SriSettingsRepository : ISriSettingsRepository
{
    private readonly ErpDbContext _db;

    public SriSettingsRepository(ErpDbContext db) => _db = db;

    public Task<SriSettings?> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        => _db.SriSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);

    public Task<SriSettings?> GetByCompanyIdForUpdateAsync(Guid companyId, CancellationToken cancellationToken = default)
        => _db.SriSettings
            .FromSqlRaw("SELECT * FROM sri_settings WHERE company_id = {0} FOR UPDATE", companyId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(SriSettings config, CancellationToken cancellationToken = default)
    {
        _db.SriSettings.Add(config);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SriSettings config, CancellationToken cancellationToken = default)
    {
        _db.SriSettings.Update(config);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
