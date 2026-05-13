using ERP.Application.Navigation.DTOs;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Garantiza que cada empresa vea en su menú resuelto (plan / personalizado / global)
/// las entradas de <b>Acceso usuarios</b> y <b>Perfiles</b>, visibles según permisos de perfil
/// (<c>access.memberships.view</c>, <c>access.profiles.view</c>) y no solo por rol Admin.
/// </summary>
public static class TenantIamMenuMerger
{
    private const string AccessRoute = "/access";
    private const string ProfilesRoute = "/profiles";
    private const string MembershipsPerm = "access.memberships.view";
    private const string ProfilesPerm = "access.profiles.view";

    public static IReadOnlyList<SessionMenuGroupDto> EnsureCompanyIamGroup(IReadOnlyList<SessionMenuGroupDto> source)
    {
        if (source is null || source.Count == 0)
            return source ?? Array.Empty<SessionMenuGroupDto>();

        var needAccess = !ContainsRouteOrPermission(source, AccessRoute, MembershipsPerm);
        var needProfiles = !ContainsRouteOrPermission(source, ProfilesRoute, ProfilesPerm);
        if (!needAccess && !needProfiles)
            return source;

        var items = new List<SessionMenuItemDto>();
        var order = 0;
        if (needAccess)
        {
            items.Add(
                new SessionMenuItemDto(
                    AccessRoute,
                    "app.nav.access",
                    null,
                    order++,
                    "access",
                    MembershipsPerm,
                    null,
                    null,
                    null,
                    null));
        }

        if (needProfiles)
        {
            items.Add(
                new SessionMenuItemDto(
                    ProfilesRoute,
                    "app.nav.profiles",
                    null,
                    order++,
                    "access",
                    ProfilesPerm,
                    null,
                    null,
                    null,
                    null));
        }

        var injected = new SessionMenuGroupDto(
            "tenant-company-iam",
            "👤",
            "app.nav.group.companyIam",
            950,
            "access",
            null,
            false,
            items,
            null);

        var merged = source.ToList();
        merged.Add(injected);
        return merged;
    }

    private static bool ContainsRouteOrPermission(
        IReadOnlyList<SessionMenuGroupDto> groups,
        string route,
        string permissionKey)
    {
        var r = NormalizeRoute(route);
        var pk = NormalizePerm(permissionKey);
        foreach (var g in groups)
        {
            if (WalkItems(g.Items, r, pk))
                return true;
        }

        return false;
    }

    private static bool WalkItems(IReadOnlyList<SessionMenuItemDto> items, string routeNorm, string permNorm)
    {
        foreach (var it in items)
        {
            if (it.Children is { Count: > 0 } && WalkItems(it.Children, routeNorm, permNorm))
                return true;

            if (NormalizeRoute(it.RoutePath) == routeNorm)
                return true;

            if (!string.IsNullOrEmpty(permNorm))
            {
                if (NormalizePerm(it.PermissionKey) == permNorm)
                    return true;
                if (it.PermissionKeysAny is not null)
                {
                    foreach (var k in it.PermissionKeysAny)
                    {
                        if (NormalizePerm(k) == permNorm)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private static string NormalizeRoute(string? path)
    {
        var p = (path ?? string.Empty).Trim();
        if (p.Length == 0)
            return string.Empty;
        if (!p.StartsWith('/'))
            p = "/" + p;
        if (p.Length > 1 && p.EndsWith('/'))
            p = p[..^1];
        return p;
    }

    private static string NormalizePerm(string? key)
    {
        var k = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (k.StartsWith("perm:", StringComparison.Ordinal))
            k = k[5..];
        return k;
    }
}
