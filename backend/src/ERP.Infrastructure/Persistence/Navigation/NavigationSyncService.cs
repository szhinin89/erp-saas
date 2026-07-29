using ERP.Domain.Kernel;
using ERP.Domain.Navigation.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Persistence.Navigation;

/// <summary>
/// Sincroniza <c>ui_nav_groups</c> y <c>ui_nav_items</c> con <see cref="KernelRegistry"/>
/// al arrancar la aplicación. Reemplaza el uso de <c>HasData()</c> para que los cambios de
/// navegación surtan efecto sin generar migraciones EF.
/// <para>
/// Reglas:
/// <list type="bullet">
///   <item>Nuevo en código → INSERT</item>
///   <item>Existente en ambos → UPDATE campos mutables</item>
///   <item>Existente en DB pero ya no en código → <c>IsActive = false</c> (no se elimina)</item>
/// </list>
/// </para>
/// </summary>
public sealed partial class NavigationSyncService
{
    private readonly ErpDbContext _db;
    private readonly ILogger<NavigationSyncService> _logger;

    public NavigationSyncService(ErpDbContext db, ILogger<NavigationSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var groupChanges = await SyncGroupsAsync(cancellationToken);
        var itemChanges = await SyncItemsAsync(cancellationToken);

        if (groupChanges + itemChanges > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            LogSyncCompleted(groupChanges, itemChanges);
        }
        else
        {
            LogSyncNoChanges();
        }
    }

    private async Task<int> SyncGroupsAsync(CancellationToken ct)
    {
        var codeGroups = KernelRegistry.Modules;
        var dbGroups = await _db.UiNavGroups.ToListAsync(ct);
        var dbById = dbGroups.ToDictionary(g => g.Id);
        var codeIds = new HashSet<Guid>();
        var changes = 0;

        foreach (var m in codeGroups)
        {
            codeIds.Add(m.GroupId);
            var labelKey = $"app.nav.group.{m.Code}";

            if (dbById.TryGetValue(m.GroupId, out var existing))
            {
                if (
                    existing.Icon != m.Icon
                    || existing.LabelKey != labelKey
                    || existing.SortOrder != m.SortOrder
                    || existing.ModuleKey != m.Code
                    || !existing.IsActive
                )
                {
                    existing.SyncFrom(m.Icon, labelKey, m.SortOrder, m.Code);
                    changes++;
                }
            }
            else
            {
                var group = UiNavGroup.Create(
                    m.GroupId,
                    m.Code,
                    m.Icon,
                    labelKey,
                    m.SortOrder,
                    m.Code,
                    rolesCsv: null,
                    requirePlatformPanel: false
                );
                _db.UiNavGroups.Add(group);
                changes++;
            }
        }

        foreach (var db in dbGroups)
        {
            if (!codeIds.Contains(db.Id) && db.IsActive)
            {
                db.Deactivate();
                changes++;
            }
        }

        return changes;
    }

    private async Task<int> SyncItemsAsync(CancellationToken ct)
    {
        var codeItems = KernelRegistry.Navigation;
        var dbItems = await _db.UiNavItems.ToListAsync(ct);
        var dbById = dbItems.ToDictionary(i => i.Id);
        var codeIds = new HashSet<Guid>();
        var changes = 0;

        foreach (var n in codeItems)
        {
            codeIds.Add(n.Id);

            if (dbById.TryGetValue(n.Id, out var existing))
            {
                if (
                    existing.GroupId != n.GroupId
                    || existing.RoutePath != n.RoutePath
                    || existing.LabelKey != n.LabelKey
                    || existing.SortOrder != n.SortOrder
                    || existing.ModuleKey != n.GroupCode
                    || existing.PermissionKey != n.PermissionKey
                    || existing.PermissionKeysAnyJson != n.PermissionKeysAnyJson
                    || existing.ParentItemId != n.ParentItemId
                    || !existing.IsActive
                )
                {
                    existing.SyncFrom(
                        n.GroupId,
                        n.RoutePath,
                        n.LabelKey,
                        n.SortOrder,
                        n.GroupCode,
                        n.PermissionKey,
                        n.PermissionKeysAnyJson,
                        n.ParentItemId
                    );
                    changes++;
                }
            }
            else
            {
                var item = UiNavItem.Create(
                    n.Id,
                    n.GroupId,
                    n.RoutePath,
                    n.LabelKey,
                    n.SortOrder,
                    n.GroupCode,
                    n.PermissionKey,
                    n.PermissionKeysAnyJson,
                    rolesCsv: null,
                    isActive: true,
                    parentItemId: n.ParentItemId
                );
                _db.UiNavItems.Add(item);
                changes++;
            }
        }

        foreach (var db in dbItems)
        {
            if (!codeIds.Contains(db.Id) && db.IsActive)
            {
                db.Deactivate();
                changes++;
            }
        }

        return changes;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Navigation sync completed: {GroupChanges} group(s), {ItemChanges} item(s) updated."
    )]
    private partial void LogSyncCompleted(int groupChanges, int itemChanges);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Navigation sync: no changes detected.")]
    private partial void LogSyncNoChanges();
}
