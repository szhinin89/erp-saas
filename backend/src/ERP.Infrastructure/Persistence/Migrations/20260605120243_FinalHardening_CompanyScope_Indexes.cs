using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalHardening_CompanyScope_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_payment_applications_company",
                table: "payment_applications",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_document_company",
                table: "expense_document",
                columns: new[] { "subscriber_id", "company_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payment_applications_company",
                table: "payment_applications");

            migrationBuilder.DropIndex(
                name: "ix_expense_document_company",
                table: "expense_document");
        }
    }
}
