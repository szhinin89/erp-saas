using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UiNavigationMenuTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ui_nav_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    module_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    roles_csv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    require_superadmin_panel = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ui_nav_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ui_nav_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    label_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    module_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    permission_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    permission_keys_any_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    roles_csv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ui_nav_items", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_groups_code",
                table: "ui_nav_groups",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_group_id_route_path",
                table: "ui_nav_items",
                columns: new[] { "group_id", "route_path" });

            // Seed alineado con frontend/src/nav/navConfig.ts (etiquetas = label_key i18n).
            var gHome = new Guid("F2D0CA10-0000-4000-8000-000000000001");
            var gCatalog = new Guid("F2D0CA10-0000-4000-8000-000000000002");
            var gAccounting = new Guid("F2D0CA10-0000-4000-8000-000000000003");
            var gAccess = new Guid("F2D0CA10-0000-4000-8000-000000000004");
            var gSecurity = new Guid("F2D0CA10-0000-4000-8000-000000000005");
            var gSaas = new Guid("F2D0CA10-0000-4000-8000-000000000006");

            migrationBuilder.InsertData(
                table: "ui_nav_groups",
                columns: new[]
                {
                    "Id", "code", "icon", "label_key", "sort_order", "module_key", "roles_csv", "require_superadmin_panel",
                    "is_active",
                },
                values: new object[,]
                {
                    { gHome, "home", "⊞", "app.nav.group.home", 0, null, null, false, true },
                    { gCatalog, "catalog", "📦", "app.nav.group.catalog", 1, "catalog", null, false, true },
                    { gAccounting, "accounting", "📒", "app.nav.group.accounting", 2, "accounting", null, false, true },
                    { gAccess, "access", "🔐", "app.nav.group.access", 3, null, "Admin,SuperAdmin", false, true },
                    { gSecurity, "security", "🛡️", "app.nav.group.security", 4, null, "SuperAdmin", true, true },
                    { gSaas, "saas", "🏢", "app.nav.group.saas", 5, null, "SuperAdmin", true, true },
                });

            migrationBuilder.InsertData(
                table: "ui_nav_items",
                columns: new[]
                {
                    "Id", "group_id", "route_path", "label_key", "sort_order", "module_key", "permission_key",
                    "permission_keys_any_json", "roles_csv", "is_active",
                },
                values: new object[,]
                {
                    { new Guid("F2D0CA11-0000-4000-8000-000000000001"), gHome, "/dashboard", "app.nav.dashboard", 0, null, null, null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000002"), gCatalog, "/products", "app.nav.products", 0, null, "catalog.products.view", null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000003"), gCatalog, "/catalog/customers", "app.nav.catalog.customers", 1, null, "catalog.customers.view", null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000004"), gCatalog, "/catalog/brands", "app.nav.catalog.brands", 2, null, "catalog.brands.view", null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000005"), gCatalog, "/catalog/product-types", "app.nav.catalog.productTypes", 3, null, "catalog.productTypes.view", null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000006"), gCatalog, "/catalog/units", "app.nav.catalog.units", 4, null, "catalog.units.view", null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000007"), gCatalog, "/catalog/tax-rates", "app.nav.catalog.taxRates", 5, null, "catalog.taxRates.view", null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000008"), gCatalog, "/catalog/tariffs", "app.nav.catalog.tariffs", 6, null, "catalog.tariffs.view", null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000009"), gCatalog, "/catalog/structure", "app.nav.catalog.structure", 7, null, "catalog.categories.view", null, null, true },
                    {
                        new Guid("F2D0CA11-0000-4000-8000-000000000010"), gAccounting, "/accounting", "app.nav.accounting", 0, null, null,
                        "[\"accounting.accounts.view\",\"accounting.journal.view\"]", null, true,
                    },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000011"), gAccess, "/access", "app.nav.access", 0, null, null, null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000012"), gAccess, "/profiles", "app.nav.profiles", 1, null, null, null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000013"), gAccess, "/saas/branches", "app.nav.branches", 2, "saas", "saas.branches.view", null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000014"), gSecurity, "/security", "app.nav.security", 0, null, null, null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000015"), gSaas, "/companies", "app.nav.companies", 0, null, null, null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000016"), gSaas, "/superadmin", "app.nav.superadmin", 1, null, null, null, null, true },
                    { new Guid("F2D0CA11-0000-4000-8000-000000000017"), gSaas, "/superadmin/forms", "app.nav.superadmin.forms", 2, null, null, null, null, true },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ui_nav_items");

            migrationBuilder.DropTable(
                name: "ui_nav_groups");
        }
    }
}
