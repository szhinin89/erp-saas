using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxSnapshotToPurchaseReception : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "discount",
                table: "purchase_reception_lines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "discount_pct",
                table: "purchase_reception_lines",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<string>(
                name: "ice_code",
                table: "purchase_reception_lines",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "line_subtotal",
                table: "purchase_reception_lines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<string>(
                name: "tax_code",
                table: "purchase_reception_lines",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<decimal>(
                name: "tax_value",
                table: "purchase_reception_lines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "total_line",
                table: "purchase_reception_lines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<string>(
                name: "vat_code",
                table: "purchase_reception_lines",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<decimal>(
                name: "vat_percentage",
                table: "purchase_reception_lines",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<string>(
                name: "doc_type_code",
                table: "purchase_reception_documents",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "sri_payment_method_code",
                table: "purchase_reception_documents",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "discount", table: "purchase_reception_lines");

            migrationBuilder.DropColumn(name: "discount_pct", table: "purchase_reception_lines");

            migrationBuilder.DropColumn(name: "ice_code", table: "purchase_reception_lines");

            migrationBuilder.DropColumn(name: "line_subtotal", table: "purchase_reception_lines");

            migrationBuilder.DropColumn(name: "tax_code", table: "purchase_reception_lines");

            migrationBuilder.DropColumn(name: "tax_value", table: "purchase_reception_lines");

            migrationBuilder.DropColumn(name: "total_line", table: "purchase_reception_lines");

            migrationBuilder.DropColumn(name: "vat_code", table: "purchase_reception_lines");

            migrationBuilder.DropColumn(name: "vat_percentage", table: "purchase_reception_lines");

            migrationBuilder.DropColumn(
                name: "doc_type_code",
                table: "purchase_reception_documents"
            );

            migrationBuilder.DropColumn(
                name: "sri_payment_method_code",
                table: "purchase_reception_documents"
            );
        }
    }
}
