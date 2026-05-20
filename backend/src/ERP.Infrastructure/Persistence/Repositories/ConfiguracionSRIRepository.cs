using Microsoft.EntityFrameworkCore;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SriSettingsRepository : ISriSettingsRepository
{
    private readonly ErpDbContext _context;

    public SriSettingsRepository(ErpDbContext context) => _context = context;

    public Task<SriSettings?> GetBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default)
        => _context.SriSettings.FirstOrDefaultAsync(c => c.SubscriberId == subscriberId, ct);

    public Task AddAsync(SriSettings config, CancellationToken ct = default)
        => _context.SriSettings.AddAsync(config, ct).AsTask();

    public Task UpdateAsync(SriSettings config, CancellationToken ct = default)
    {
        _context.SriSettings.Update(config);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}