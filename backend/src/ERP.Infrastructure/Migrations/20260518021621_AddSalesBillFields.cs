using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesBillFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "product_code",
                table: "sales_note_line",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "vat_code",
                table: "sales_note_line",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "0");

            migrationBuilder.AddColumn<decimal>(
                name: "vat_percentage",
                table: "sales_note_line",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "sales_bill_line",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "product_code",
                table: "sales_bill_line",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "vat_code",
                table: "sales_bill_line",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "0");

            migrationBuilder.AddColumn<decimal>(
                name: "vat_percentage",
                table: "sales_bill_line",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "sales_bill",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "payment_days",
                table: "sales_bill",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "payment_method_code",
                table: "sales_bill",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "01");

            migrationBuilder.AddColumn<decimal>(
                name: "total_discount",
                table: "sales_bill",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "product_code",
                table: "sales_note_line");

            migrationBuilder.DropColumn(
                name: "vat_code",
                table: "sales_note_line");

            migrationBuilder.DropColumn(
                name: "vat_percentage",
                table: "sales_note_line");

            migrationBuilder.DropColumn(
                name: "discount_amount",
                table: "sales_bill_line");

            migrationBuilder.DropColumn(
                name: "product_code",
                table: "sales_bill_line");

            migrationBuilder.DropColumn(
                name: "vat_code",
                table: "sales_bill_line");

            migrationBuilder.DropColumn(
                name: "vat_percentage",
                table: "sales_bill_line");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "sales_bill");

            migrationBuilder.DropColumn(
                name: "payment_days",
                table: "sales_bill");

            migrationBuilder.DropColumn(
                name: "payment_method_code",
                table: "sales_bill");

            migrationBuilder.DropColumn(
                name: "total_discount",
                table: "sales_bill");
        }
    }
}
