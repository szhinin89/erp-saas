using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Configuration;

public sealed class ConfigurationChangeLogQueryRepository : IConfigurationChangeLogQueryRepository
{
    private readonly ErpDbContext _db;

    public ConfigurationChangeLogQueryRepository(ErpDbContext db) => _db = db;

    public async Task<(IReadOnlyList<ConfigurationChangeLog> Items, int TotalCount)> GetAsync(
        ConfigurationChangeLogQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var q = _db
            .ConfigurationChangeLogs.IgnoreQueryFilters()
            .Where(l => l.TenantId == query.TenantId && l.CompanyId == query.CompanyId);

        if (!string.IsNullOrWhiteSpace(query.EntityType))
            q = q.Where(l => l.EntityType == query.EntityType);
        if (query.EntityId is not null)
            q = q.Where(l => l.EntityId == query.EntityId);
        if (!string.IsNullOrWhiteSpace(query.Key))
            q = q.Where(l => l.Key == query.Key);
        if (query.Scope is not null)
            q = q.Where(l => l.Scope == query.Scope);
        if (query.FromUtc is not null)
            q = q.Where(l => l.ChangedAtUtc >= query.FromUtc);
        if (query.ToUtc is not null)
            q = q.Where(l => l.ChangedAtUtc <= query.ToUtc);

        var total = await q.CountAsync(cancellationToken);

        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var page = Math.Max(query.Page, 1);

        var items = await q.OrderByDescending(l => l.ChangedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
