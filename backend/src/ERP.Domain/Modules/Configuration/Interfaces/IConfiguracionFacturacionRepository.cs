using ERP.Domain.Configuration.Entities;

namespace ERP.Domain.Configuration.Interfaces;

public interface IBillingSettingsRepository
{
    Task<BillingSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(BillingSettings config, CancellationToken ct = default);
    Task UpdateAsync(BillingSettings config, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
