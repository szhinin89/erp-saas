using ERP.Domain.Configuration.Entities;

namespace ERP.Domain.Configuration.Interfaces;

public interface IConfiguracionSRIRepository
{
    Task<ConfiguracionSRI?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(ConfiguracionSRI config, CancellationToken ct = default);
    Task UpdateAsync(ConfiguracionSRI config, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}