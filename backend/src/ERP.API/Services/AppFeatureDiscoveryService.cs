using System.Reflection;
using ERP.API.Attributes;
using ERP.Domain.Modules.Menu.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ERP.API.Services;

/// <summary>
/// Discovers <see cref="AppFeatureAttribute"/> on controllers/actions and syncs the <c>AppFeatures</c> table.
/// Skips rows whose <see cref="AppFeatureAttribute.Permission"/> starts with <c>SuperAdmin</c> (case-insensitive).
/// </summary>
public sealed class AppFeatureDiscoveryService
{
    private readonly IAppFeatureRepository _repository;
    private readonly ILogger<AppFeatureDiscoveryService> _logger;

    public AppFeatureDiscoveryService(IAppFeatureRepository repository, ILogger<AppFeatureDiscoveryService> logger)
    {
        _repository = repository;
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

        var syncRows = byPerm.Values
            .Select(r => new AppFeatureSyncRow(
                r.Permission,
                r.Name,
                r.Icon,
                r.Path,
                r.ParentPermission,
                r.SortOrder,
                r.IsVisibleInMenu,
                r.IsSuperAdmin))
            .ToList();

        return await _repository.SyncDiscoveredFeaturesAsync(syncRows, ct);
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
