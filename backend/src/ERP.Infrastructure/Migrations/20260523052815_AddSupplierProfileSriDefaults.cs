using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierProfileSriDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "tax_support_code",
                table: "master_supplier_profiles",
                newName: "default_tax_support_code");

            migrationBuilder.AddColumn<string>(
                name: "default_retention_income_code",
                table: "master_supplier_profiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_retention_vat_code",
                table: "master_supplier_profiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default_retention_income_code",
                table: "master_supplier_profiles");

            migrationBuilder.DropColumn(
                name: "default_retention_vat_code",
                table: "master_supplier_profiles");

            migrationBuilder.RenameColumn(
                name: "default_tax_support_code",
                table: "master_supplier_profiles",
                newName: "tax_support_code");
        }
    }
}
