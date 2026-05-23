using ERP.Domain.Modules.Menu.Entities;
using ERP.Domain.Modules.Menu.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class AppFeatureRepository : IAppFeatureRepository
{
    private readonly ErpDbContext _db;
    private readonly ILogger<AppFeatureRepository> _logger;

    public AppFeatureRepository(ErpDbContext db, ILogger<AppFeatureRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AppFeatureMenuRow>> ListVisibleMenuRowsAsync(CancellationToken ct = default)
        => await _db.AppFeatures
            .AsNoTracking()
            .Where(x => x.IsVisibleInMenu)
            .Select(x => new AppFeatureMenuRow(
                x.Id,
                x.Name,
                x.Icon,
                x.Path,
                x.Permission,
                x.ParentId,
                x.SortOrder))
            .ToListAsync(ct);

    public async Task<int> SyncDiscoveredFeaturesAsync(IReadOnlyList<AppFeatureSyncRow> rows, CancellationToken ct = default)
    {
        var byPerm = new Dictionary<string, AppFeatureSyncRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows.OrderBy(r => r.ParentPermission is null ? 0 : 1).ThenBy(r => r.SortOrder).ThenBy(r => r.Permission, StringComparer.Ordinal))
            byPerm[r.Permission] = r;

        var utc = DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var tracked = await _db.AppFeatures.AsTracking()
            .ToDictionaryAsync(x => x.Permission, x => x, StringComparer.OrdinalIgnoreCase, ct);

        var permToId = tracked.ToDictionary(x => x.Key, x => x.Value.Id, StringComparer.OrdinalIgnoreCase);

        var pending = byPerm.Values.ToList();
        var guard = 0;
        while (pending.Count > 0 && guard++ < 64)
        {
            var batch = pending
                .Where(r => string.IsNullOrEmpty(r.ParentPermission) || permToId.ContainsKey(r.ParentPermission))
                .ToList();
            if (batch.Count == 0)
            {
                foreach (var orphan in pending)
                    _logger.LogWarning(
                        "AppFeature with unresolved parent (skipped): {Permission} -> parent {Parent}",
                        orphan.Permission,
                        orphan.ParentPermission);
                break;
            }

            foreach (var r in batch)
            {
                pending.Remove(r);
                Guid? parentId = null;
                if (!string.IsNullOrEmpty(r.ParentPermission) && permToId.TryGetValue(r.ParentPermission, out var pid))
                    parentId = pid;

                if (tracked.TryGetValue(r.Permission, out var entity))
                {
                    entity.SyncFromDiscovery(r.Name, r.Icon, r.Path, parentId, r.SortOrder, r.IsVisibleInMenu, r.IsPlatformOnlyFeature, utc);
                }
                else
                {
                    var created = AppFeature.Create(
                        r.Name,
                        r.Icon,
                        r.Path,
                        r.Permission,
                        parentId,
                        r.SortOrder,
                        r.IsVisibleInMenu,
                        r.IsPlatformOnlyFeature,
                        utc);
                    _db.AppFeatures.Add(created);
                    tracked[r.Permission] = created;
                    permToId[r.Permission] = created.Id;
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        _logger.LogInformation("AppFeature sync completed ({Count} unique permissions).", byPerm.Count);
        return byPerm.Count;
    }
}
