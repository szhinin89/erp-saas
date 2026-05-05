using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UiNavMoveAccessToConfiguracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE ui_nav_items
                SET group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1),
                    roles_csv = 'Admin,SuperAdmin'
                WHERE route_path = '/access'
                  AND EXISTS (SELECT 1 FROM ui_nav_groups WHERE code = 'configuracion');

                UPDATE ui_nav_items SET sort_order = 0
                WHERE route_path = '/access'
                  AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1);

                UPDATE ui_nav_items SET sort_order = 1
                WHERE route_path = '/superadmin/forms'
                  AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1);

                UPDATE ui_nav_items SET sort_order = 2
                WHERE route_path = '/saas/branches'
                  AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1);

                UPDATE ui_nav_items SET sort_order = 3
                WHERE route_path = '/profiles'
                  AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1);

                UPDATE ui_nav_groups g
                SET is_active = false
                WHERE g.code = 'access'
                  AND NOT EXISTS (
                    SELECT 1 FROM ui_nav_items i
                    WHERE i.group_id = g."Id" AND i.is_active = true
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE ui_nav_groups SET is_active = true WHERE code = 'access';

                UPDATE ui_nav_items
                SET group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'access' LIMIT 1),
                    roles_csv = NULL
                WHERE route_path = '/access'
                  AND EXISTS (SELECT 1 FROM ui_nav_groups WHERE code = 'access');

                UPDATE ui_nav_items SET sort_order = 0
                WHERE route_path = '/access'
                  AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'access' LIMIT 1);

                UPDATE ui_nav_items SET sort_order = 0
                WHERE route_path = '/superadmin/forms'
                  AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1);

                UPDATE ui_nav_items SET sort_order = 1
                WHERE route_path = '/saas/branches'
                  AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1);

                UPDATE ui_nav_items SET sort_order = 2
                WHERE route_path = '/profiles'
                  AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1);
                """);
        }
    }
}
