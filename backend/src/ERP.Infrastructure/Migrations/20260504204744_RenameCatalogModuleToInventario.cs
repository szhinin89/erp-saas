using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameCatalogModuleToInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE ui_nav_groups
                SET code = 'inventario',
                    module_key = 'inventario',
                    label_key = 'app.nav.group.inventario'
                WHERE code = 'catalog';

                UPDATE ui_nav_groups
                SET module_key = 'inventario'
                WHERE code = 'sales' AND module_key = 'catalog';

                UPDATE ui_nav_items
                SET route_path = replace(route_path, '/catalog/', '/inventario/')
                WHERE route_path LIKE '/catalog/%';

                UPDATE ui_nav_items
                SET permission_key = replace(permission_key, 'catalog.', 'inventario.')
                WHERE permission_key LIKE 'catalog.%';

                UPDATE ui_nav_items
                SET permission_keys_any_json = replace(permission_keys_any_json, 'catalog.', 'inventario.')
                WHERE permission_keys_any_json IS NOT NULL
                  AND permission_keys_any_json LIKE '%catalog.%';

                UPDATE access_profile_permissions
                SET permission_key = replace(permission_key, 'catalog.', 'inventario.')
                WHERE permission_key LIKE 'catalog.%';

                UPDATE tenants
                SET enabled_modules = replace(enabled_modules, '"catalog"', '"inventario"')
                WHERE enabled_modules IS NOT NULL
                  AND enabled_modules LIKE '%"catalog"%';

                UPDATE saas_feature_definitions
                SET resource_ref = replace(resource_ref, 'catalog.', 'inventario.')
                WHERE resource_ref IS NOT NULL
                  AND resource_ref LIKE 'catalog.%';

                UPDATE user_activity
                SET module = 'inventario'
                WHERE module = 'catalog';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE user_activity
                SET module = 'catalog'
                WHERE module = 'inventario';

                UPDATE saas_feature_definitions
                SET resource_ref = replace(resource_ref, 'inventario.', 'catalog.')
                WHERE resource_ref IS NOT NULL
                  AND resource_ref LIKE 'inventario.%';

                UPDATE tenants
                SET enabled_modules = replace(enabled_modules, '"inventario"', '"catalog"')
                WHERE enabled_modules IS NOT NULL
                  AND enabled_modules LIKE '%"inventario"%';

                UPDATE access_profile_permissions
                SET permission_key = replace(permission_key, 'inventario.', 'catalog.')
                WHERE permission_key LIKE 'inventario.%';

                UPDATE ui_nav_items
                SET permission_keys_any_json = replace(permission_keys_any_json, 'inventario.', 'catalog.')
                WHERE permission_keys_any_json IS NOT NULL
                  AND permission_keys_any_json LIKE '%inventario.%';

                UPDATE ui_nav_items
                SET permission_key = replace(permission_key, 'inventario.', 'catalog.')
                WHERE permission_key LIKE 'inventario.%';

                UPDATE ui_nav_items
                SET route_path = replace(route_path, '/inventario/', '/catalog/')
                WHERE route_path LIKE '/inventario/%';

                UPDATE ui_nav_groups
                SET module_key = 'catalog'
                WHERE code = 'sales' AND module_key = 'inventario';

                UPDATE ui_nav_groups
                SET code = 'catalog',
                    module_key = 'catalog',
                    label_key = 'app.nav.group.catalog'
                WHERE code = 'inventario';
                """);
        }
    }
}
