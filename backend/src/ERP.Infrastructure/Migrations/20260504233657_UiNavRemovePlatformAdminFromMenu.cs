using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UiNavRemovePlatformAdminFromMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plataforma SuperAdmin (/superadmin*, /companies): no forman parte del menú por-tenant en BD.
            // Los enlaces viven solo en frontend (navConfig) para no mezclarlos con módulos de clientes.
            migrationBuilder.Sql(
                """
                UPDATE ui_nav_items SET is_active = false
                WHERE route_path LIKE '/superadmin%' OR route_path = '/companies';

                UPDATE ui_nav_groups SET is_active = false
                WHERE code = 'saas'
                  AND NOT EXISTS (
                    SELECT 1 FROM ui_nav_items i
                    WHERE i.group_id = ui_nav_groups."Id" AND i.is_active = true
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE ui_nav_groups SET is_active = true WHERE code = 'saas';

                UPDATE ui_nav_items SET is_active = true
                WHERE route_path LIKE '/superadmin%' OR route_path = '/companies';
                """);
        }
    }
}
