using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseCreditNoteSupplierNumberUniqueExcludesCancelled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_purchase_credit_notes_tenant_company_supplier_number",
                table: "purchase_credit_notes");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_company_supplier_number",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "credit_note_number" },
                unique: true,
                filter: "\"status\" <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_purchase_credit_notes_tenant_company_supplier_number",
                table: "purchase_credit_notes");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_company_supplier_number",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "credit_note_number" },
                unique: true);
        }
    }
}
