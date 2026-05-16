using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantOperationalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "tenants",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<int>(
                name: "default_credit_days",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "invoice_prefix",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "language",
                table: "tenants",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "es");

            migrationBuilder.AddColumn<string>(
                name: "timezone",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "America/Guayaquil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "currency",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "default_credit_days",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "invoice_prefix",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "language",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "timezone",
                table: "tenants");
        }
    }
}
