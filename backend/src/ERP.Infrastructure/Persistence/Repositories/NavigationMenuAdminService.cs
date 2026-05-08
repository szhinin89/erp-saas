using ERP.Application.Navigation;
using ERP.Application.Navigation.DTOs;
using ERP.Domain.Navigation.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Navigation;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class NavigationMenuAdminService : INavigationMenuAdminService
{
    private readonly ErpDbContext _db;

    public NavigationMenuAdminService(ErpDbContext db) => _db = db;

    public async Task<AdminNavigationMenuResponse> GetMenuTreeAsync(CancellationToken ct = default)
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
        var seenLevelKeys = new HashSet<(Guid GroupId, Guid? ParentItemId)>();
        var itemParent = new Dictionary<Guid, Guid?>();
        var itemGroup = new Dictionary<Guid, Guid>();
        var itemSort = new Dictionary<Guid, int>();

        foreach (var level in levels)
        {
            if (!seenLevelKeys.Add((level.GroupId, level.ParentItemId)))
                return (false, "Nivel duplicado (mismo grupo y mismo padre).");

            for (var i = 0; i < level.OrderedItemIds.Count; i++)
            {
                var id = level.OrderedItemIds[i];
                if (itemParent.ContainsKey(id))
                    return (false, "Un ítem aparece en más de un nivel.");

                itemParent[id] = level.ParentItemId;
                itemGroup[id] = level.GroupId;
                itemSort[id] = i;
            }
        }

        if (itemParent.Count == 0)
            return (true, null);

        var groupIds = itemGroup.Values.Distinct().ToList();
        var dbItems = await _db.UiNavItems.AsNoTracking()
            .Where(i => i.IsActive && groupIds.Contains(i.GroupId))
            .Select(i => new { i.Id, i.GroupId })
            .ToListAsync(ct);

        if (dbItems.Count != itemParent.Count)
            return (false, "La estructura debe incluir todos los ítems activos de los grupos afectados.");

        foreach (var row in dbItems)
        {
            if (!itemGroup.TryGetValue(row.Id, out var g) || g != row.GroupId)
                return (false, "Un ítem no coincide con el grupo en base de datos.");
        }

        foreach (var level in levels)
        {
            if (level.ParentItemId is { } pid)
            {
                if (!itemParent.ContainsKey(pid))
                    return (false, "Un padre referenciado no está incluido en la estructura.");
                if (itemGroup[pid] != level.GroupId)
                    return (false, "El padre no pertenece al mismo grupo.");
            }
        }

        foreach (var startId in itemParent.Keys)
        {
            var visited = new HashSet<Guid>();
            Guid? cur = startId;
            while (cur is not null)
            {
                var id = cur.Value;
                if (!visited.Add(id))
                    return (false, "Se detectó un ciclo en la jerarquía del menú.");
                if (!itemParent.TryGetValue(id, out var p))
                    break;
                cur = p;
            }
        }

        await using var tr = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var id in itemParent.Keys)
            {
                var newParent = itemParent[id];
                var sort = itemSort[id];
                await _db.UiNavItems.Where(x => x.Id == id)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(x => x.ParentItemId, newParent).SetProperty(x => x.SortOrder, sort),
                        ct);
            }

            await tr.CommitAsync(ct);
        }
        catch
        {
            await tr.RollbackAsync(ct);
            throw;
        }

        return (true, null);
    }

    public async Task<(bool Ok, Guid? NewId, string? Error)> CreateNavItemAsync(
        CreateNavItemRequest request,
        CancellationToken ct = default)
    {
        var route = (request.RoutePath ?? string.Empty).Trim();
        if (route.Length == 0 || !route.StartsWith('/'))
            return (false, null, "La ruta debe empezar por /.");

        var label = (request.DisplayLabel ?? string.Empty).Trim();
        if (label.Length == 0 || label.Length > 200)
            return (false, null, "El texto a mostrar es obligatorio (máx. 200 caracteres).");

        var groupOk = await _db.UiNavGroups.AsNoTracking()
            .AnyAsync(g => g.Id == request.GroupId && g.IsActive, ct);
        if (!groupOk)
            return (false, null, "Grupo no encontrado o inactivo.");

        if (request.ParentItemId is { } pid)
        {
            var parentOk = await _db.UiNavItems.AsNoTracking()
                .AnyAsync(i => i.Id == pid && i.GroupId == request.GroupId && i.IsActive, ct);
            if (!parentOk)
                return (false, null, "El ítem padre no existe o no pertenece al grupo.");
        }

        var routeTaken = await _db.UiNavItems.AsNoTracking()
            .AnyAsync(i => i.GroupId == request.GroupId && i.RoutePath == route && i.IsActive, ct);
        if (routeTaken)
            return (false, null, "Ya existe un ítem activo con esa ruta en el grupo.");

        var maxOrder = await _db.UiNavItems.AsNoTracking()
            .Where(i => i.GroupId == request.GroupId && i.ParentItemId == request.ParentItemId && i.IsActive)
            .Select(i => (int?)i.SortOrder)
            .MaxAsync(ct) ?? -1;

        if (request.SaasFeatureDefinitionId is { } sfid)
        {
            var featOk = await _db.SaasFeatureDefinitions.AsNoTracking().AnyAsync(f => f.Id == sfid, ct);
            if (!featOk)
                return (false, null, "Definición SaaS no encontrada.");
        }

        var labelKey = $"nav.custom.{Guid.NewGuid():N}";
        var id = Guid.NewGuid();
        var entity = UiNavItem.Create(
            id,
            request.GroupId,
            route,
            labelKey,
            maxOrder + 1,
            request.ModuleKey,
            request.PermissionKey,
            null,
            null,
            true,
            request.ParentItemId,
            label,
            request.SaasFeatureDefinitionId);

        _db.UiNavItems.Add(entity);
        await _db.SaveChangesAsync(ct);
        return (true, id, null);
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
                i.DisplayLabel,
                i.SortOrder,
                i.ModuleKey,
                i.PermissionKey,
                ParseKeysAny(i.PermissionKeysAnyJson),
                i.IsActive,
                i.SaasFeatureDefinitionId,
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
