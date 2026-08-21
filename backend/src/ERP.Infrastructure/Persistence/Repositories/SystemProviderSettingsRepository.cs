using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SystemProviderSettingsRepository : ISystemProviderSettingsRepository
{
    private readonly ErpDbContext _db;

    public SystemProviderSettingsRepository(ErpDbContext db) => _db = db;

    public Task<SystemProviderSettings?> GetAsync(CancellationToken cancellationToken = default) =>
        _db.SystemProviderSettings.FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(
        SystemProviderSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        _db.SystemProviderSettings.Add(settings);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
