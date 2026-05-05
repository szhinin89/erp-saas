using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UiNavMoveProfilesToConfiguracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE ui_nav_items
                SET group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1),
                    roles_csv = 'Admin,SuperAdmin'
                WHERE route_path = '/profiles'
                  AND EXISTS (SELECT 1 FROM ui_nav_groups WHERE code = 'configuracion');

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE ui_nav_items
                SET group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'access' LIMIT 1),
                    roles_csv = NULL
                WHERE route_path = '/profiles'
                  AND EXISTS (SELECT 1 FROM ui_nav_groups WHERE code = 'access');

                UPDATE ui_nav_items SET sort_order = 1
                WHERE route_path = '/profiles'
                  AND group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'access' LIMIT 1);
                """);
        }
    }
}
