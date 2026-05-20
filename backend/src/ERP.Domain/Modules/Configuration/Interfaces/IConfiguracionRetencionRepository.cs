using ERP.Domain.Configuration.Entities;

namespace ERP.Domain.Configuration.Interfaces;

public interface IRetentionSettingsRepository
{
    Task<IReadOnlyList<RetentionSettings>> GetActiveForSupplierAsync(Guid subscriberId, CancellationToken ct = default);
    Task AddAsync(RetentionSettings entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
