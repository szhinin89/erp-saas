using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupplierPaymentReversalSemantics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "reversal_bank_reason",
                table: "supplier_payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "reversal_cash_not_delivered_confirmed",
                table: "supplier_payments",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reversal_bank_reason",
                table: "supplier_payments");

            migrationBuilder.DropColumn(
                name: "reversal_cash_not_delivered_confirmed",
                table: "supplier_payments");
        }
    }
}
