using ERP.Domain.Configuration.Entities;

namespace ERP.Domain.Configuration.Interfaces;

public interface ISriSettingsRepository
{
    Task<SriSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(SriSettings config, CancellationToken ct = default);
    Task UpdateAsync(SriSettings config, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}