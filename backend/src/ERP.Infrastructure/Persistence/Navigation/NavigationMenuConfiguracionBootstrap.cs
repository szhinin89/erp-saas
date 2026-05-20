using System.Text.Json;
using System.Text.Json.Nodes;
using ERP.Domain.Navigation.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Navigation;

/// <summary>
/// Asegura en BD el grupo <c>settings</c> y el orden justo después de Inventory.
/// Idempotente: corrige entornos con <c>sort_order</c> desactualizado.
/// </summary>
public static class NavigationMenuConfiguracionBootstrap
{
    private static readonly Guid SettingsGroupId = Guid.Parse("f2d0ca10-0000-4000-8000-000000000008");

    private const string RealignItemsAndSortSql =
        """
        UPDATE ui_nav_groups SET is_active = true WHERE code = 'settings';

        -- Garantiza los 3 ítems de configuración con IDs fijos (idempotente).
        -- sort_order alineado a la convención 10/20/30 del script 003.
        INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
        SELECT '00000000-0000-4000-8000-000000000101', g."Id",
               '/settings/company', 'app.nav.item.settings.company', 'Datos de Empresa',
               10, 'settings', 'settings.company.view', true
        FROM ui_nav_groups g WHERE g.code = 'settings'
        ON CONFLICT ("Id") DO UPDATE SET
            route_path     = '/settings/company',
            permission_key = 'settings.company.view',
            module_key     = 'settings';

        INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
        SELECT '00000000-0000-4000-8000-000000000104', g."Id",
               '/saas/companies', 'app.nav.item.saas.companies', 'Empresas operativas',
               5, 'settings', 'saas.companies.view', true
        FROM ui_nav_groups g WHERE g.code = 'settings'
        ON CONFLICT ("Id") DO UPDATE SET
            route_path     = '/saas/companies',
            permission_key = 'saas.companies.view',
            module_key     = 'settings';

        INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
        SELECT '00000000-0000-4000-8000-000000000102', g."Id",
               '/settings/sri', 'app.nav.item.settings.sri', 'Configuración SRI',
               20, 'settings', 'settings.sri.view', true
        FROM ui_nav_groups g WHERE g.code = 'settings'
        ON CONFLICT ("Id") DO UPDATE SET
            route_path     = '/settings/sri',
            permission_key = 'settings.sri.view',
            module_key     = 'settings';

        INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
        SELECT '00000000-0000-4000-8000-000000000103', g."Id",
               '/settings/ride', 'app.nav.item.settings.ride', 'Configuración RIDE',
               30, 'settings', 'settings.ride.view', true
        FROM ui_nav_groups g WHERE g.code = 'settings'
        ON CONFLICT ("Id") DO UPDATE SET
            route_path     = '/settings/ride',
            permission_key = 'settings.ride.view',
            module_key     = 'settings';
        """;

    // ── JSON de la carpeta "Configuración" que se inyecta en cada plan ──────────────
    // labelKey usa un UUID fijo para que sea idempotente entre reinicios.
    private const string ConfigFolderLabelKey  = "nav.planFolder.a1b2c3d4-cfg";
    private static readonly string[] ConfigRoutes =
    [
        "/settings/company",
        "/settings/sri",
        "/settings/ride",
        // legacy routes (tolerated during migration window)
        "/configuracion/empresa",
        "/configuracion/sri",
        "/configuracion/facturacion",
    ];

    private static readonly (string route, string label, string perm, string icon, string leafKey)[] ConfigLeaves =
    [
        ("/saas/companies",   "Empresas operativas", "perm:saas.companies.view", "apartment",    "nav.planLeaf.saas-companies"),
        ("/settings/company", "Datos de Empresa",   "perm:settings.company.view", "business",     "nav.planLeaf.cfg-empresa"),
        ("/settings/sri",     "Configuración SRI",  "perm:settings.sri.view",     "receipt_long", "nav.planLeaf.cfg-sri"),
        ("/settings/ride",    "Configuración RIDE", "perm:settings.ride.view",    "print",        "nav.planLeaf.cfg-ride"),
    ];

    public static async Task EnsureAsync(ErpDbContext db, CancellationToken ct = default)
    {
        var hasGroup = await db.UiNavGroups.AsNoTracking().AnyAsync(g => g.Code == "settings", ct);
        if (!hasGroup)
        {
            db.UiNavGroups.Add(
                UiNavGroup.Create(
                    SettingsGroupId,
                    "settings",
                    "⚙",
                    "app.nav.group.settings",
                    20,
                    null,
                    null,
                    requireSuperAdminPanel: false,
                    isActive: true));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
            }
        }

        await db.Database.ExecuteSqlRawAsync(RealignItemsAndSortSql, cancellationToken: ct);
        await EnsureConfiguracionFolderInPlansAsync(db, ct);
    }

    /// <summary>
    /// Garantiza que todos los planes activos tengan la carpeta "Configuración" con los 3 formularios
    /// en su MenuConfigJson. Idempotente: si la carpeta ya existe con esos ítems no hace nada.
    /// Elimina los mismos ítems si aparecen sueltos fuera de la carpeta (evita duplicados).
    /// </summary>
    private static async Task EnsureConfiguracionFolderInPlansAsync(ErpDbContext db, CancellationToken ct)
    {
        var plans = await db.CommercialPlans.Where(p => p.IsActive).ToListAsync(ct);
        var opts = new JsonSerializerOptions { WriteIndented = true };
        var changed = false;

        foreach (var plan in plans)
        {
            var raw = plan.MenuConfigJson;
            JsonArray groups;

            if (string.IsNullOrWhiteSpace(raw))
            {
                // Plan sin JSON → crear estructura mínima con la carpeta
                groups = BuildDefaultPlanGroup(plan.Name ?? plan.Code ?? "plan");
                plan.SetMenuConfigJson(groups.ToJsonString(opts));
                changed = true;
                continue;
            }

            JsonNode? root;
            try { root = JsonNode.Parse(raw); }
            catch { continue; } // JSON inválido — no tocar

            groups = root as JsonArray ?? new JsonArray();

            // Si el plan ya contiene /settings/company en cualquier grupo (estructura
            // multi-grupo real), no inyectar la carpeta plan-custom de configuración.
            if (AnyGroupContainsConfigRoute(groups)) continue;

            // Buscar o crear el grupo "plan-custom"
            JsonObject? customGroup = null;
            foreach (var g in groups)
            {
                if (g is JsonObject jo)
                {
                    var code = (jo["code"] ?? jo["Code"])?.GetValue<string>() ?? "";
                    if (code == "plan-custom") { customGroup = jo; break; }
                }
            }

            if (customGroup is null)
            {
                customGroup = BuildCustomGroupObject(plan.Name ?? plan.Code ?? "plan");
                groups.Add(customGroup);
            }

            var items = customGroup["items"] as JsonArray ?? new JsonArray();
            customGroup["items"] = items;

            // ¿Ya existe la carpeta de configuración dentro de plan-custom?
            if (HasConfigFolder(items)) continue;

            // Eliminar ítems sueltos de configuración para evitar duplicados
            RemoveLooseConfigItems(items);

            // Agregar carpeta al inicio (sort 0) y reordenar el resto
            var folder = BuildConfigFolder();
            // Incrementar sortOrder de los demás
            foreach (var it in items)
            {
                if (it is JsonObject jo)
                {
                    var so = jo["sortOrder"]?.GetValue<int>() ?? 0;
                    jo["sortOrder"] = so + 1;
                }
            }
            // Insertar al principio
            var newItems = new JsonArray();
            newItems.Add(folder);
            foreach (var it in items) newItems.Add(it?.DeepClone());
            customGroup["items"] = newItems;

            plan.SetMenuConfigJson(groups.ToJsonString(opts));
            changed = true;
        }

        if (changed) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Devuelve true si CUALQUIER grupo del plan ya contiene /settings/company
    /// (directamente o como hijo de carpeta). Usado para detectar planes con
    /// estructura multi-grupo real que no deben recibir la carpeta plan-custom.
    /// </summary>
    private static bool AnyGroupContainsConfigRoute(JsonArray topLevelGroups)
    {
        foreach (var g in topLevelGroups)
        {
            if (g is not JsonObject go) continue;
            var groupItems = go["items"] as JsonArray ?? new JsonArray();
            if (HasConfigFolder(groupItems)) return true;
            foreach (var item in groupItems)
            {
                if (item is JsonObject io)
                {
                    var route = io["routePath"]?.GetValue<string>() ?? "";
                    if (route == "/settings/company" || route == "/configuracion/empresa")
                        return true;
                }
            }
        }
        return false;
    }

    private static bool HasConfigFolder(JsonArray items)
    {
        foreach (var item in items)
        {
            if (item is not JsonObject jo) continue;
            var lk = jo["labelKey"]?.GetValue<string>() ?? "";
            if (lk == ConfigFolderLabelKey) return true;
            // También detectar por children que contengan /configuracion/empresa
            if (jo["children"] is JsonArray ch)
            {
                foreach (var c in ch)
                {
                    if (c is JsonObject cjo)
                    {
                        var route = cjo["routePath"]?.GetValue<string>() ?? "";
                        if (route == "/settings/company" || route == "/configuracion/empresa") return true;
                    }
                }
            }
        }
        return false;
    }

    private static void RemoveLooseConfigItems(JsonArray items)
    {
        var toRemove = new List<int>();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is JsonObject jo)
            {
                var route = jo["routePath"]?.GetValue<string>() ?? "";
                if (ConfigRoutes.Contains(route)) toRemove.Add(i);
            }
        }
        for (var i = toRemove.Count - 1; i >= 0; i--)
            items.RemoveAt(toRemove[i]);
    }

    private static JsonObject BuildConfigFolder()
    {
        var children = new JsonArray();
        for (var i = 0; i < ConfigLeaves.Length; i++)
        {
            var (route, label, perm, icon, leafKey) = ConfigLeaves[i];
            children.Add(new JsonObject
            {
                ["routePath"]         = route,
                ["labelKey"]          = leafKey,
                ["displayLabel"]      = label,
                ["sortOrder"]         = i,
                ["moduleKey"]         = null,
                ["permissionKey"]     = perm,
                ["permissionKeysAny"] = null,
                ["itemRoles"]         = null,
                ["icon"]              = icon,
                ["children"]          = null,
            });
        }

        return new JsonObject
        {
            ["routePath"]         = "",
            ["labelKey"]          = ConfigFolderLabelKey,
            ["displayLabel"]      = "Configuración",
            ["sortOrder"]         = 0,
            ["moduleKey"]         = null,
            ["permissionKey"]     = null,
            ["permissionKeysAny"] = null,
            ["itemRoles"]         = null,
            ["icon"]              = "settings",
            ["children"]          = children,
        };
    }

    private static JsonObject BuildCustomGroupObject(string planName)
        => new()
        {
            ["code"]                  = "plan-custom",
            ["icon"]                  = "layers",
            ["labelKey"]              = $"nav.plan.{planName.ToLowerInvariant()}",
            ["sortOrder"]             = 5,
            ["moduleKey"]             = null,
            ["roles"]                 = null,
            ["requireSuperAdminPanel"]= false,
            ["menuBarLayout"]         = "horizontal",
            ["items"]                 = new JsonArray(),
        };

    private static JsonArray BuildDefaultPlanGroup(string planName)
    {
        var group = BuildCustomGroupObject(planName);
        group["items"] = new JsonArray { BuildConfigFolder() };
        return [group];
    }
}
