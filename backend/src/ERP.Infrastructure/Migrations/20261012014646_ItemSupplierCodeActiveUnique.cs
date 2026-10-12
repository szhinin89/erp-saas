using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ItemSupplierCodeActiveUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_item_supplier_codes_tenant_company_supplier_code",
                table: "item_supplier_codes");

            migrationBuilder.CreateIndex(
                name: "uq_item_supplier_codes_tenant_company_supplier_code",
                table: "item_supplier_codes",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "code" },
                unique: true,
                filter: "is_active = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_item_supplier_codes_tenant_company_supplier_code",
                table: "item_supplier_codes");

            migrationBuilder.CreateIndex(
                name: "uq_item_supplier_codes_tenant_company_supplier_code",
                table: "item_supplier_codes",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "code" },
                unique: true);
        }
    }
}
