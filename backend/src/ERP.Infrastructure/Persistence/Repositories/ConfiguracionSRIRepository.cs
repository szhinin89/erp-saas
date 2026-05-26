using Microsoft.EntityFrameworkCore;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SriSettingsRepository : ISriSettingsRepository
{
    private readonly ErpDbContext _context;

    public SriSettingsRepository(ErpDbContext context) => _context = context;

    public Task<SriSettings?> GetByCompanyIdAsync(Guid companyId, CancellationToken ct = default)
        => _context.SriSettings.FirstOrDefaultAsync(c => c.CompanyId == companyId, ct);

    public Task<SriSettings?> GetByCompanyIdForUpdateAsync(Guid companyId, CancellationToken ct = default)
        => _context.SriSettings
            .FromSqlRaw("SELECT * FROM sri_settings WHERE company_id = {0} FOR UPDATE", companyId)
            .FirstOrDefaultAsync(ct);

    public Task AddAsync(SriSettings config, CancellationToken ct = default)
        => _context.SriSettings.AddAsync(config, ct).AsTask();

    public Task UpdateAsync(SriSettings config, CancellationToken ct = default)
    {
        _context.SriSettings.Update(config);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}