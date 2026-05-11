using ERP.Domain.Configuration.Entities;

namespace ERP.Domain.Configuration.Interfaces;

public interface IConfiguracionFacturacionRepository
{
    Task<ConfiguracionFacturacion?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(ConfiguracionFacturacion config, CancellationToken ct = default);
    Task UpdateAsync(ConfiguracionFacturacion config, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
