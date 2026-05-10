using Microsoft.EntityFrameworkCore;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class ConfiguracionSRIRepository : IConfiguracionSRIRepository
{
    private readonly ErpDbContext _context;

    public ConfiguracionSRIRepository(ErpDbContext context) => _context = context;

    public Task<ConfiguracionSRI?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
        => _context.ConfiguracionSRIs.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);

    public Task AddAsync(ConfiguracionSRI config, CancellationToken ct = default)
        => _context.ConfiguracionSRIs.AddAsync(config, ct).AsTask();

    public Task UpdateAsync(ConfiguracionSRI config, CancellationToken ct = default)
    {
        _context.ConfiguracionSRIs.Update(config);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}