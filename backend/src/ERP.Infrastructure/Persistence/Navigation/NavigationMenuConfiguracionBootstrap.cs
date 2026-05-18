using System.Text.Json;
using System.Text.Json.Nodes;
using ERP.Domain.Navigation.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Navigation;

/// <summary>
/// Asegura en BD el grupo <c>configuracion</c> y el orden justo después de Inventario
/// (ancla <c>inventario</c> o, si no existe, <c>catalog</c> en BDs sin renombre aplicado).
/// Idempotente: corrige entornos sin migración aplicada o con <c>sort_order</c> desactualizado.
/// </summary>
public static class NavigationMenuConfiguracionBootstrap
{
    private static readonly Guid ConfiguracionGroupId = Guid.Parse("f2d0ca10-0000-4000-8000-000000000008");

    /// <summary>
    /// Reactiva el grupo, reasigna ítems y coloca <c>configuracion</c> justo después de inventario/catálogo.
    /// </summary>
    private const string RealignItemsAndSortSql =
        """
        UPDATE ui_nav_groups SET is_active = true WHERE code = 'configuracion';

        UPDATE ui_nav_items
        SET group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1),
            roles_csv = CASE
                WHEN route_path = '/saas/branches' THEN 'Admin,SuperAdmin'
                WHEN route_path = '/profiles' THEN NULL
                WHEN route_path = '/access' THEN NULL
                ELSE roles_csv
            END,
            permission_key = CASE
                WHEN route_path = '/profiles' THEN 'access.profiles.view'
                WHEN route_path = '/access' THEN 'access.memberships.view'
                ELSE permission_key
            END
        WHERE route_path IN ('/saas/branches', '/profiles', '/access')
          AND EXISTS (SELECT 1 FROM ui_nav_groups WHERE code = 'configuracion')
          AND group_id IS DISTINCT FROM (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1);

        UPDATE ui_nav_items SET sort_order = 0
        WHERE route_path = '/access'
          AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1);
        UPDATE ui_nav_items SET sort_order = 1
        WHERE route_path = '/saas/branches'
          AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1);
        UPDATE ui_nav_items SET sort_order = 2
        WHERE route_path = '/profiles'
          AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1);

        -- Ítems de configuración SRI/facturación (idempotente: INSERT si no existe)
        INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
        SELECT
            '00000000-0000-4000-8000-000000000101',
            g."Id",
            '/configuracion/empresa',
            'app.nav.item.config.empresa',
            'Datos de Empresa',
            10,
            'ventas',
            'ventas.configuracion.view',
            true
        FROM ui_nav_groups g WHERE g.code = 'configuracion'
        ON CONFLICT ("Id") DO NOTHING;

        INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
        SELECT
            '00000000-0000-4000-8000-000000000102',
            g."Id",
            '/configuracion/sri',
            'app.nav.item.config.sri',
            'Configuración SRI',
            11,
            'ventas',
            'ventas.configuracion.view',
            true
        FROM ui_nav_groups g WHERE g.code = 'configuracion'
        ON CONFLICT ("Id") DO NOTHING;

        INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
        SELECT
            '00000000-0000-4000-8000-000000000103',
            g."Id",
            '/configuracion/facturacion',
            'app.nav.item.config.facturacion',
            'Configuración RIDE',
            12,
            'ventas',
            'ventas.configuracion.view',
            true
        FROM ui_nav_groups g WHERE g.code = 'configuracion'
        ON CONFLICT ("Id") DO NOTHING;

        DO $$
        DECLARE
          inv_so integer;
          conf_so integer;
        BEGIN
          SELECT sort_order INTO inv_so FROM ui_nav_groups WHERE code = 'inventario' LIMIT 1;
          IF inv_so IS NULL THEN
            SELECT sort_order INTO inv_so FROM ui_nav_groups WHERE code = 'catalog' LIMIT 1;
          END IF;
          SELECT sort_order INTO conf_so FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1;
          IF inv_so IS NULL OR conf_so IS NULL THEN
            RETURN;
          END IF;
          IF conf_so = inv_so + 1 THEN
            RETURN;
          END IF;

          UPDATE ui_nav_groups
          SET sort_order = sort_order + 1
          WHERE sort_order > inv_so
            AND code <> 'configuracion';

          UPDATE ui_nav_groups
          SET sort_order = inv_so + 1
          WHERE code = 'configuracion';
        END $$;
        """;

    // ── JSON de la carpeta "Configuración" que se inyecta en cada plan ──────────────
    // labelKey usa un UUID fijo para que sea idempotente entre reinicios.
    private const string ConfigFolderLabelKey  = "nav.planFolder.a1b2c3d4-cfg";
    private static readonly string[] ConfigRoutes =
    [
        "/configuracion/empresa",
        "/configuracion/sri",
        "/configuracion/facturacion",
    ];

    private static readonly (string route, string label, string perm, string icon, string leafKey)[] ConfigLeaves =
    [
        ("/configuracion/empresa",     "Datos de Empresa",    "perm:ventas.configuracion.view", "business",     "nav.planLeaf.cfg-empresa"),
        ("/configuracion/sri",         "Configuración SRI",   "perm:ventas.configuracion.view", "receipt_long", "nav.planLeaf.cfg-sri"),
        ("/configuracion/facturacion", "Configuración RIDE",  "perm:ventas.configuracion.view", "print",        "nav.planLeaf.cfg-ride"),
    ];

    public static async Task EnsureAsync(ErpDbContext db, CancellationToken ct = default)
    {
        var hasGroup = await db.UiNavGroups.AsNoTracking().AnyAsync(g => g.Code == "configuracion", ct);
        if (!hasGroup)
        {
            db.UiNavGroups.Add(
                UiNavGroup.Create(
                    ConfiguracionGroupId,
                    "configuracion",
                    "⚙",
                    "app.nav.group.configuracion",
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
        var plans = await db.SaasPlans.Where(p => p.IsActive).ToListAsync(ct);
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

            // ¿Ya existe la carpeta de configuración?
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
                        if (route == "/configuracion/empresa") return true;
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
