using Microsoft.EntityFrameworkCore;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class BillingSettingsRepository : IBillingSettingsRepository
{
    private readonly ErpDbContext _context;

    public BillingSettingsRepository(ErpDbContext context) => _context = context;

    public Task<SubscriberBillingProfile?> GetBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default)
        => _context.SubscriberBillingProfiles.FirstOrDefaultAsync(c => c.SubscriberId == subscriberId, ct);

    public Task AddAsync(SubscriberBillingProfile profile, CancellationToken ct = default)
        => _context.SubscriberBillingProfiles.AddAsync(profile, ct).AsTask();

    public Task UpdateAsync(SubscriberBillingProfile profile, CancellationToken ct = default)
    {
        _context.SubscriberBillingProfiles.Update(profile);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
