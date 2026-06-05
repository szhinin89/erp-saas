using ERP.Domain.Configuration.Entities;

namespace ERP.Domain.Configuration.Interfaces;

public interface ISubscriberBillingProfileRepository
{
    Task<SubscriberBillingProfile?> GetBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default);
    Task AddAsync(SubscriberBillingProfile profile, CancellationToken ct = default);
    Task UpdateAsync(SubscriberBillingProfile profile, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
