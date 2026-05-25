using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RelaxSalesNoteOriginalBillFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sales_note_sales_bill_original_bill_id",
                table: "sales_note");

            migrationBuilder.DropIndex(
                name: "IX_sales_note_original_bill_id",
                table: "sales_note");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_sales_note_original_bill_id",
                table: "sales_note",
                column: "original_bill_id");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_note_sales_bill_original_bill_id",
                table: "sales_note",
                column: "original_bill_id",
                principalTable: "sales_bill",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
