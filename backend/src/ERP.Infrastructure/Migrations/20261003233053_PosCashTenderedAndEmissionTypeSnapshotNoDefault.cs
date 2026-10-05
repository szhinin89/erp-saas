using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PosCashTenderedAndEmissionTypeSnapshotNoDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<short>(
                name: "emission_type",
                table: "sales_invoices",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldDefaultValue: (short)1
            );

            migrationBuilder.AddColumn<decimal>(
                name: "tendered_amount",
                table: "sales_invoice_payments",
                type: "numeric(18,2)",
                nullable: true
            );

            migrationBuilder.AddCheckConstraint(
                name: "chk_sales_invoice_payments_tendered_covers_amount",
                table: "sales_invoice_payments",
                sql: "\"tendered_amount\" IS NULL OR \"tendered_amount\" >= \"amount\""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_sales_invoice_payments_tendered_covers_amount",
                table: "sales_invoice_payments"
            );

            migrationBuilder.DropColumn(name: "tendered_amount", table: "sales_invoice_payments");

            migrationBuilder.AlterColumn<short>(
                name: "emission_type",
                table: "sales_invoices",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1,
                oldClrType: typeof(short),
                oldType: "smallint"
            );
        }
    }
}
