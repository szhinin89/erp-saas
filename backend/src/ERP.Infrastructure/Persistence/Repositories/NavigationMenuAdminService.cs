using ERP.Application.Navigation;
using ERP.Application.Navigation.DTOs;
using ERP.Domain.Navigation.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class NavigationMenuAdminService : INavigationMenuAdminService
{
    private readonly ErpDbContext _db;

    public NavigationMenuAdminService(ErpDbContext db) => _db = db;

    public async Task<AdminNavigationMenuResponse> GetMenuTreeAsync(CancellationToken ct = default)
    {
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

        var byGroup = items.GroupBy(i => i.GroupId).ToDictionary(g => g.Key, g => g.ToList());

        var groupDtos = new List<AdminNavGroupRowDto>();
        foreach (var g in groups)
        {
            var list = byGroup.TryGetValue(g.Id, out var row) ? row : [];
            groupDtos.Add(new AdminNavGroupRowDto(
                g.Id,
                g.Code,
                g.Icon,
                g.LabelKey,
                g.SortOrder,
                g.ModuleKey,
                ParseRoles(g.RolesCsv),
                g.RequireSuperAdminPanel,
                g.IsActive,
                BuildAdminItemTree(list, null)));
        }

        return new AdminNavigationMenuResponse(groupDtos);
    }

    public async Task<(bool Ok, string? Error)> ReorderGroupsAsync(
        IReadOnlyList<Guid> orderedGroupIds,
        CancellationToken ct = default)
    {
        var activeIds = await _db.UiNavGroups.AsNoTracking()
            .Where(g => g.IsActive)
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (orderedGroupIds.Count != activeIds.Count)
            return (false, "La lista debe incluir todos los grupos activos exactamente una vez.");

        if (orderedGroupIds.Distinct().Count() != orderedGroupIds.Count)
            return (false, "Hay ids de grupo duplicados.");

        var activeSet = activeIds.ToHashSet();
        foreach (var id in orderedGroupIds)
        {
            if (!activeSet.Contains(id))
                return (false, "Un id de grupo no corresponde a un grupo activo.");
        }

        for (var i = 0; i < orderedGroupIds.Count; i++)
        {
            var id = orderedGroupIds[i];
            await _db.UiNavGroups.Where(g => g.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.SortOrder, i), ct);
        }

        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> ReorderItemLevelsAsync(
        IReadOnlyList<NavItemSiblingOrderDto> levels,
        CancellationToken ct = default)
    {
        foreach (var level in levels)
        {
            var siblings = await _db.UiNavItems.AsNoTracking()
                .Where(i => i.GroupId == level.GroupId && i.ParentItemId == level.ParentItemId && i.IsActive)
                .Select(i => i.Id)
                .ToListAsync(ct);

            if (siblings.Count != level.OrderedItemIds.Count)
                return (false, $"Cantidad de ítems incorrecta para grupo {level.GroupId} padre {level.ParentItemId}.");

            if (level.OrderedItemIds.Distinct().Count() != level.OrderedItemIds.Count)
                return (false, "Hay ids de ítem duplicados en un nivel.");

            var siblingSet = siblings.ToHashSet();
            foreach (var id in level.OrderedItemIds)
            {
                if (!siblingSet.Contains(id))
                    return (false, "Un id de ítem no pertenece al nivel indicado (grupo/padre).");
            }

            for (var i = 0; i < level.OrderedItemIds.Count; i++)
            {
                var id = level.OrderedItemIds[i];
                await _db.UiNavItems.Where(x => x.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, i), ct);
            }
        }

        return (true, null);
    }

    private static IReadOnlyList<AdminNavItemRowDto> BuildAdminItemTree(
        IReadOnlyList<UiNavItem> groupItems,
        Guid? parentItemId)
    {
        var children = groupItems
            .Where(i => i.ParentItemId == parentItemId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.RoutePath, StringComparer.Ordinal)
            .ToList();

        var list = new List<AdminNavItemRowDto>();
        foreach (var i in children)
        {
            var nested = BuildAdminItemTree(groupItems, i.Id);
            list.Add(new AdminNavItemRowDto(
                i.Id,
                i.ParentItemId,
                i.RoutePath,
                i.LabelKey,
                i.SortOrder,
                i.ModuleKey,
                i.PermissionKey,
                ParseKeysAny(i.PermissionKeysAnyJson),
                i.IsActive,
                nested));
        }

        return list;
    }

    private static IReadOnlyList<string>? ParseRoles(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts.ToList();
    }

    private static IReadOnlyList<string>? ParseKeysAny(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var arr = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            return arr is { Count: > 0 } ? arr : null;
        }
        catch
        {
            return null;
        }
    }
}
