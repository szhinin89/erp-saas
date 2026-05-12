using System.Text.Json;
using ERP.Application.Navigation;
using ERP.Application.Navigation.DTOs;
using ERP.Domain.Navigation.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Navigation;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class NavigationMenuReader : INavigationMenuReader
{
    private readonly ErpDbContext _db;

    public NavigationMenuReader(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SessionMenuGroupDto>> GetActiveMenuAsync(CancellationToken ct = default)
    {
        await NavigationMenuConfiguracionBootstrap.EnsureAsync(_db, ct);

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

        var dtos = new List<SessionMenuGroupDto>();
        foreach (var g in groups)
        {
            var list = byGroup.TryGetValue(g.Id, out var row) ? row : [];
            var itemDtos = BuildSessionItemTree(list, null);

            dtos.Add(new SessionMenuGroupDto(
                g.Code,
                g.Icon,
                g.LabelKey,
                g.SortOrder,
                g.ModuleKey,
                ParseRoles(g.RolesCsv),
                g.RequireSuperAdminPanel,
                itemDtos));
        }

        return dtos;
    }

    private static IReadOnlyList<SessionMenuItemDto> BuildSessionItemTree(
        IReadOnlyList<UiNavItem> groupItems,
        Guid? parentItemId)
    {
        var children = groupItems
            .Where(i => i.ParentItemId == parentItemId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.RoutePath, StringComparer.Ordinal)
            .ToList();

        var list = new List<SessionMenuItemDto>();
        foreach (var i in children)
        {
            var nested = BuildSessionItemTree(groupItems, i.Id);
            var keysAny = ParseKeysAny(i.PermissionKeysAnyJson);
            list.Add(new SessionMenuItemDto(
                i.RoutePath,
                i.LabelKey,
                i.DisplayLabel,
                i.SortOrder,
                i.ModuleKey,
                i.PermissionKey,
                keysAny,
                ParseRoles(i.RolesCsv),
                nested.Count > 0 ? nested : null,
                null));
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
            var arr = JsonSerializer.Deserialize<List<string>>(json);
            return arr is { Count: > 0 } ? arr : null;
        }
        catch
        {
            return null;
        }
    }
}
