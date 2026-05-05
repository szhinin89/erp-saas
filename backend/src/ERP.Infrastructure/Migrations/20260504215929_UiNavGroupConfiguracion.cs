using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UiNavGroupConfiguracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO ui_nav_groups ("Id", code, icon, label_key, sort_order, module_key, roles_csv, require_superadmin_panel, is_active)
                SELECT 'f2d0ca10-0000-4000-8000-000000000008'::uuid, 'configuracion', '⚙', 'app.nav.group.configuracion', 20, NULL, NULL, false, true
                WHERE NOT EXISTS (SELECT 1 FROM ui_nav_groups WHERE code = 'configuracion');

                UPDATE ui_nav_items
                SET group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1),
                    roles_csv = CASE
                        WHEN route_path = '/superadmin/forms' THEN 'SuperAdmin'
                        WHEN route_path = '/saas/branches' THEN 'Admin,SuperAdmin'
                        ELSE roles_csv
                    END
                WHERE route_path IN ('/superadmin/forms', '/saas/branches');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE ui_nav_items
                SET group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'access' LIMIT 1),
                    roles_csv = NULL
                WHERE route_path = '/saas/branches';

                UPDATE ui_nav_items
                SET group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'saas' LIMIT 1),
                    roles_csv = NULL
                WHERE route_path = '/superadmin/forms';

                DELETE FROM ui_nav_groups WHERE code = 'configuracion';
                """);
        }
    }
}
