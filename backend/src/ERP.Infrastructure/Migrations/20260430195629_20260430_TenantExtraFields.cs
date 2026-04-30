using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _20260430_TenantExtraFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dinardap",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "logo_url",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ruc",
                table: "tenants",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "short_name",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trade_name",
                table: "tenants",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dinardap",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "display_order",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "logo_url",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ruc",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "short_name",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "trade_name",
                table: "tenants");
        }
    }
}
