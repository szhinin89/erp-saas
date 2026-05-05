using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveCustomersToVentasRoute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE access_profile_permissions
                SET permission_key = replace(permission_key, 'inventario.customers.', 'ventas.customers.')
                WHERE permission_key LIKE 'inventario.customers.%';

                UPDATE ui_nav_items
                SET permission_key = replace(permission_key, 'inventario.customers.', 'ventas.customers.')
                WHERE permission_key LIKE 'inventario.customers.%';

                UPDATE ui_nav_items
                SET permission_keys_any_json = replace(permission_keys_any_json, 'inventario.customers.', 'ventas.customers.')
                WHERE permission_keys_any_json IS NOT NULL
                  AND permission_keys_any_json LIKE '%inventario.customers.%';

                UPDATE ui_nav_items
                SET route_path = '/ventas/customers',
                    module_key = 'sales'
                WHERE route_path IN ('/inventario/customers', '/catalog/customers');

                UPDATE ui_nav_items i
                SET group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'sales' LIMIT 1)
                WHERE i.route_path = '/ventas/customers';

                UPDATE saas_feature_definitions
                SET resource_ref = replace(resource_ref, 'inventario.customers', 'ventas.customers')
                WHERE resource_ref IS NOT NULL
                  AND resource_ref LIKE '%inventario.customers%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE ui_nav_items i
                SET group_id = (SELECT "Id" FROM ui_nav_groups WHERE code = 'inventario' LIMIT 1)
                WHERE i.route_path = '/ventas/customers';

                UPDATE ui_nav_items
                SET route_path = '/inventario/customers',
                    module_key = 'inventario'
                WHERE route_path = '/ventas/customers';

                UPDATE saas_feature_definitions
                SET resource_ref = replace(resource_ref, 'ventas.customers', 'inventario.customers')
                WHERE resource_ref IS NOT NULL
                  AND resource_ref LIKE '%ventas.customers%';

                UPDATE ui_nav_items
                SET permission_keys_any_json = replace(permission_keys_any_json, 'ventas.customers.', 'inventario.customers.')
                WHERE permission_keys_any_json IS NOT NULL
                  AND permission_keys_any_json LIKE '%ventas.customers.%';

                UPDATE ui_nav_items
                SET permission_key = replace(permission_key, 'ventas.customers.', 'inventario.customers.')
                WHERE permission_key LIKE 'ventas.customers.%';

                UPDATE access_profile_permissions
                SET permission_key = replace(permission_key, 'ventas.customers.', 'inventario.customers.')
                WHERE permission_key LIKE 'ventas.customers.%';
                """);
        }
    }
}
