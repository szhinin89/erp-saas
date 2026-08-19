using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurationFoundationP0Integrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CONFIG-FOUNDATION-P0-01: NO se emite AddColumn para "xmin" — es la columna de
            // sistema que Postgres ya expone en toda tabla; Postgres además rechaza crear una
            // columna de usuario con ese nombre ("column name \"xmin\" conflicts with a system
            // column name"). OrgSettingConfiguration.cs solo declara xmin como shadow property
            // para que EF Core la use como token de concurrencia optimista en tiempo de
            // ejecución — no requiere ni admite DDL.
            migrationBuilder.CreateIndex(
                name: "uq_warehouses_tenant_branch_main",
                table: "warehouses",
                columns: new[] { "tenant_id", "company_id", "branch_id" },
                unique: true,
                filter: "is_main = true");

            migrationBuilder.CreateIndex(
                name: "uq_price_lists_tenant_company_default",
                table: "price_lists",
                columns: new[] { "tenant_id", "company_id", "is_default" },
                unique: true,
                filter: "is_default = true");

            migrationBuilder.CreateIndex(
                name: "uq_establishment_tenant_company_main",
                table: "establishment",
                columns: new[] { "tenant_id", "company_id" },
                unique: true,
                filter: "is_main = true");

            migrationBuilder.CreateIndex(
                name: "uq_emission_point_establishment_default",
                table: "emission_point",
                columns: new[] { "tenant_id", "company_id", "establishment_id" },
                unique: true,
                filter: "is_default = true");

            migrationBuilder.CreateIndex(
                name: "uq_branches_tenant_company_main",
                table: "branches",
                columns: new[] { "tenant_id", "company_id" },
                unique: true,
                filter: "is_main_branch = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_warehouses_tenant_branch_main",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "uq_price_lists_tenant_company_default",
                table: "price_lists");

            migrationBuilder.DropIndex(
                name: "uq_establishment_tenant_company_main",
                table: "establishment");

            migrationBuilder.DropIndex(
                name: "uq_emission_point_establishment_default",
                table: "emission_point");

            migrationBuilder.DropIndex(
                name: "uq_branches_tenant_company_main",
                table: "branches");
        }
    }
}
