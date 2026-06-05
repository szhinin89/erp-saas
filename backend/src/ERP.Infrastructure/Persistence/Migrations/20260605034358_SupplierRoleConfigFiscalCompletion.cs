using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplierRoleConfigFiscalCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "default_payment_method_code",
                table: "master_bp_supplier_configs",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_retention_exempt",
                table: "master_bp_supplier_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default_payment_method_code",
                table: "master_bp_supplier_configs");

            migrationBuilder.DropColumn(
                name: "is_retention_exempt",
                table: "master_bp_supplier_configs");
        }
    }
}
