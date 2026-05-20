using Microsoft.EntityFrameworkCore;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class BillingSettingsRepository : IBillingSettingsRepository
{
    private readonly ErpDbContext _context;

    public BillingSettingsRepository(ErpDbContext context) => _context = context;

    public Task<BillingSettings?> GetBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default)
        => _context.BillingSettings.FirstOrDefaultAsync(c => c.SubscriberId == subscriberId, ct);

    public Task AddAsync(BillingSettings config, CancellationToken ct = default)
        => _context.BillingSettings.AddAsync(config, ct).AsTask();

    public Task UpdateAsync(BillingSettings config, CancellationToken ct = default)
    {
        _context.BillingSettings.Update(config);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
