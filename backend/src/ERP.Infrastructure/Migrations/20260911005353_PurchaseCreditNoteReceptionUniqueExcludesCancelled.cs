using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseCreditNoteReceptionUniqueExcludesCancelled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_purchase_credit_notes_tenant_reception_document_id",
                table: "purchase_credit_notes");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_reception_document_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "reception_document_id" },
                unique: true,
                filter: "\"reception_document_id\" IS NOT NULL AND \"status\" <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_purchase_credit_notes_tenant_reception_document_id",
                table: "purchase_credit_notes");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_reception_document_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "reception_document_id" },
                unique: true,
                filter: "\"reception_document_id\" IS NOT NULL");
        }
    }
}
