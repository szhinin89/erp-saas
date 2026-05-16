using System.Reflection;
using ERP.API.Attributes;
using ERP.Domain.Modules.Menu.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Services;

/// <summary>
/// Discovers <see cref="AppFeatureAttribute"/> on controllers/actions and syncs the <c>AppFeatures</c> table.
/// Skips rows whose <see cref="AppFeatureAttribute.Permission"/> starts with <c>SuperAdmin</c> (case-insensitive).
/// </summary>
public sealed class AppFeatureDiscoveryService
{
    private readonly ErpDbContext _db;
    private readonly ILogger<AppFeatureDiscoveryService> _logger;

    public AppFeatureDiscoveryService(ErpDbContext db, ILogger<AppFeatureDiscoveryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private sealed record DiscoveryRow(
        string Permission,
        string Name,
        string? Icon,
        string? Path,
        string? ParentPermission,
        int SortOrder,
        bool IsVisibleInMenu,
        bool IsSuperAdmin);

    public async Task<int> SyncFeaturesAsync(CancellationToken ct = default)
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var rows = new List<DiscoveryRow>();
        var controllerTypes = asm.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var type in controllerTypes)
        {
            var classAttr = type.GetCustomAttribute<AppFeatureAttribute>(inherit: true);
            if (classAttr is not null && !ShouldExclude(classAttr.Permission))
                rows.Add(ToRow(classAttr, NormalizeParent(classAttr.ParentPermission)));

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!IsHttpAction(method))
                    continue;

                var methodAttr = method.GetCustomAttribute<AppFeatureAttribute>(inherit: false);
                if (methodAttr is null)
                    continue;

                if (ShouldExclude(methodAttr.Permission))
                    continue;

                string? parentPermission;
                if (!string.IsNullOrWhiteSpace(methodAttr.ParentPermission))
                    parentPermission = methodAttr.ParentPermission.Trim();
                else if (classAttr is not null && !string.IsNullOrWhiteSpace(classAttr.Permission))
                    parentPermission = classAttr.Permission.Trim();
                else
                    parentPermission = null;

                rows.Add(ToRow(methodAttr, parentPermission));
            }
        }

        var byPerm = new Dictionary<string, DiscoveryRow>(StringComparer.OrdinalIgnoreCase);
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
                    _logger.LogWarning("AppFeature with unresolved parent (skipped): {Permission} -> parent {Parent}", orphan.Permission, orphan.ParentPermission);
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
                    entity.SyncFromDiscovery(r.Name, r.Icon, r.Path, parentId, r.SortOrder, r.IsVisibleInMenu, r.IsSuperAdmin, utc);
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
                        r.IsSuperAdmin,
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

    private static string? NormalizeParent(string? parent) =>
        string.IsNullOrWhiteSpace(parent) ? null : parent.Trim();

    private static bool ShouldExclude(string permission)
    {
        var p = (permission ?? string.Empty).Trim();
        return p.StartsWith("SuperAdmin", StringComparison.OrdinalIgnoreCase);
    }

    private static DiscoveryRow ToRow(AppFeatureAttribute a, string? parentPermissionExplicit)
    {
        var perm = (a.Permission ?? string.Empty).Trim();
        var parent = NormalizeParent(parentPermissionExplicit);
        return new DiscoveryRow(
            perm,
            a.Name,
            a.Icon,
            a.Path,
            parent,
            a.SortOrder,
            a.IsVisibleInMenu,
            a.IsSuperAdmin);
    }

    private static bool IsHttpAction(MethodInfo m)
    {
        if (m.IsSpecialName || m.ContainsGenericParameters)
            return false;
        return m.GetCustomAttributes(inherit: true).Any(static a => a is HttpMethodAttribute);
    }
}
