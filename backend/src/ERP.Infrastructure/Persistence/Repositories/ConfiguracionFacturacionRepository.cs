using Microsoft.EntityFrameworkCore;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class ConfiguracionFacturacionRepository : IConfiguracionFacturacionRepository
{
    private readonly ErpDbContext _context;

    public ConfiguracionFacturacionRepository(ErpDbContext context) => _context = context;

    public Task<ConfiguracionFacturacion?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
        => _context.ConfiguracionFacturaciones.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);

    public Task AddAsync(ConfiguracionFacturacion config, CancellationToken ct = default)
        => _context.ConfiguracionFacturaciones.AddAsync(config, ct).AsTask();

    public Task UpdateAsync(ConfiguracionFacturacion config, CancellationToken ct = default)
    {
        _context.ConfiguracionFacturaciones.Update(config);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
