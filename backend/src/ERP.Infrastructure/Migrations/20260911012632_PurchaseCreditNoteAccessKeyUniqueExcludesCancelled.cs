using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseCreditNoteAccessKeyUniqueExcludesCancelled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_purchase_credit_notes_tenant_access_key",
                table: "purchase_credit_notes");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_access_key",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "\"access_key\" IS NOT NULL AND \"status\" <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_purchase_credit_notes_tenant_access_key",
                table: "purchase_credit_notes");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_access_key",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "\"access_key\" IS NOT NULL");
        }
    }
}
