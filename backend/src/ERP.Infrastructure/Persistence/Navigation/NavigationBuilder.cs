using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Application.Navigation.DTOs;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Navigation.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ERP.Infrastructure.Persistence.Navigation;

/// <summary>
/// Construye el menú lateral filtrado por permisos efectivos del usuario.
/// Cache en memoria: clave nav:menu:{tenantId}:{companyId}:{userId} — TTL 5 min.
/// Los permisos efectivos dependen de TenantId + CompanyId + UserId (un usuario puede
/// tener roles/permisos distintos por empresa), por lo que CompanyId forma parte de la clave.
/// CompanyId se resuelve siempre vía <see cref="ICompanyContextProvider"/> (contexto de
/// sesión autenticada), nunca se toma directamente de un valor sin validar.
/// Admin role: ve todos los ítems activos sin restricción.
/// </summary>
public sealed class NavigationBuilder : INavigationBuilder
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const string CachePrefix = "nav:menu";

    private readonly ErpDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly ICompanyContextProvider _companyCtx;
    private readonly IEffectivePermissionKeysProvider _permKeys;
    private readonly IMemoryCache _cache;

    public NavigationBuilder(
        ErpDbContext db,
        ICurrentTenant tenant,
        ICurrentUser user,
        ICompanyContextProvider companyCtx,
        IEffectivePermissionKeysProvider permKeys,
        IMemoryCache cache)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _companyCtx = companyCtx;
        _permKeys = permKeys;
        _cache = cache;
    }

    public void InvalidateCache(Guid tenantId, Guid companyId, Guid userId)
        => _cache.Remove(BuildCacheKey(tenantId, companyId, userId));

    private static string BuildCacheKey(Guid tenantId, Guid companyId, Guid userId)
        => $"{CachePrefix}:{tenantId}:{companyId}:{userId}";

    public async Task<IReadOnlyList<NavMenuGroupDto>> BuildMenuAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenant.TenantId;
        var userId = _user.UserId;
        var role = _user.Role ?? string.Empty;

        // CompanyId del contexto operativo de la sesión autenticada (membresía validada),
        // nunca un valor crudo enviado por el frontend sin validar.
        var ctx = userId != Guid.Empty
            ? await _companyCtx.ResolveOperationalForCurrentUserAsync(cancellationToken)
            : null;
        var companyId = ctx?.CompanyId ?? Guid.Empty;

        var cacheKey = BuildCacheKey(tenantId, companyId, userId);

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<NavMenuGroupDto>? cached) && cached is not null)
            return cached;

        var result = await BuildInternalAsync(tenantId, userId, role, ctx, cancellationToken);

        _cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    private async Task<IReadOnlyList<NavMenuGroupDto>> BuildInternalAsync(
        Guid tenantId, Guid userId, string role, OperationalCompanyContext? ctx, CancellationToken ct)
    {
        var isAdmin = string.Equals(role, SecurityRoles.Admin, StringComparison.OrdinalIgnoreCase);

        // Resolve effective permissions for non-admin users.
        HashSet<string>? allowed = null;
        if (!isAdmin && userId != Guid.Empty)
        {
            if (ctx?.ProfileId is not null && ctx.IsActiveMembership)
            {
                var keys = await _permKeys.GetAllowedKeysAsync(
                    tenantId, ctx.CompanyId, userId, ctx.ProfileId.Value, ct);
                allowed = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            }
        }

        var groups = await _db.UiNavGroups.AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Code)
            .ToListAsync(ct);

        var items = await _db.UiNavItems.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.RoutePath)
            .ToListAsync(ct);

        var byGroup = items.GroupBy(i => i.GroupId)
                          .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<NavMenuGroupDto>();

        foreach (var g in groups)
        {
            if (!IsGroupVisible(g, role)) continue;

            var groupItems = byGroup.TryGetValue(g.Id, out var list) ? list : [];
            var itemDtos = BuildItemTree(groupItems, null, role, allowed, isAdmin);

            if (itemDtos.Count == 0) continue;

            result.Add(new NavMenuGroupDto(g.Id, g.Code, g.Icon, g.LabelKey, itemDtos));
        }

        return result;
    }

    private static bool IsGroupVisible(UiNavGroup g, string role)
    {
        if (string.IsNullOrWhiteSpace(g.RolesCsv)) return true;
        var roles = g.RolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    }

    private static List<NavMenuItemDto> BuildItemTree(
        IReadOnlyList<UiNavItem> groupItems,
        Guid? parentId,
        string role,
        HashSet<string>? allowed,
        bool isAdmin)
    {
        var siblings = groupItems
            .Where(i => i.ParentItemId == parentId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.RoutePath, StringComparer.Ordinal);

        var result = new List<NavMenuItemDto>();
        foreach (var item in siblings)
        {
            if (!IsItemVisible(item, role, allowed, isAdmin)) continue;

            var children = BuildItemTree(groupItems, item.Id, role, allowed, isAdmin);
            result.Add(new NavMenuItemDto(
                item.Id,
                item.LabelKey,
                item.DisplayLabel,
                item.RoutePath,
                children));
        }

        return result;
    }

    private static bool IsItemVisible(UiNavItem item, string role, HashSet<string>? allowed, bool isAdmin)
    {
        // Role restriction check.
        if (!string.IsNullOrWhiteSpace(item.RolesCsv))
        {
            var roles = item.RolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        // Admin bypasses permission checks.
        if (isAdmin) return true;

        // Items with no permission requirement are visible to all authenticated users.
        var hasSingle = !string.IsNullOrWhiteSpace(item.PermissionKey);
        var hasAny = !string.IsNullOrWhiteSpace(item.PermissionKeysAnyJson);

        if (!hasSingle && !hasAny) return true;

        if (allowed is null) return false;

        if (hasSingle && allowed.Contains(item.PermissionKey!)) return true;

        if (hasAny)
        {
            var keysAny = ParseKeysAny(item.PermissionKeysAnyJson);
            if (keysAny?.Any(k => allowed.Contains(k)) == true) return true;
        }

        return false;
    }

    private static List<string>? ParseKeysAny(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<string>>(json); }
        catch { return null; }
    }
}
