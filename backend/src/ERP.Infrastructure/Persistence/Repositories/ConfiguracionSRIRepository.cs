using Microsoft.EntityFrameworkCore;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class ConfiguracionSRIRepository : ISriSettingsRepository
{
    private readonly ErpDbContext _context;

    public ConfiguracionSRIRepository(ErpDbContext context) => _context = context;

    public Task<SriSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
        => _context.SriSettings.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);

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