using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCodeToSalesBillLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Snapshot del SaleCode del producto en líneas de factura y nota.
            // NOT NULL con default vacío para filas históricas existentes.
            migrationBuilder.AddColumn<string>(
                name: "product_code",
                table: "sales_bill_line",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "product_code",
                table: "sales_note_line",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "product_code",
                table: "sales_bill_line");

            migrationBuilder.DropColumn(
                name: "product_code",
                table: "sales_note_line");
        }
    }
}
