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
                WHEN route_path = '/profiles' THEN 'Admin,SuperAdmin'
                WHEN route_path = '/access' THEN 'Admin,SuperAdmin'
                ELSE roles_csv
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
    }
}
