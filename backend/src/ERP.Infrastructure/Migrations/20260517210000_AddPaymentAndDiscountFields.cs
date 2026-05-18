using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAndDiscountFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // sales_bill — forma de pago, plazo y descuento total
            migrationBuilder.AddColumn<string>(
                name: "payment_method_code",
                table: "sales_bill",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "01");

            migrationBuilder.AddColumn<short>(
                name: "payment_days",
                table: "sales_bill",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<decimal>(
                name: "total_discount",
                table: "sales_bill",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // sales_bill_line — descuento por línea
            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "sales_bill_line",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "payment_method_code", table: "sales_bill");
            migrationBuilder.DropColumn(name: "payment_days",        table: "sales_bill");
            migrationBuilder.DropColumn(name: "total_discount",      table: "sales_bill");
            migrationBuilder.DropColumn(name: "discount_amount",     table: "sales_bill_line");
        }
    }
}
