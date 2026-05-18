using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVatCodeToSalesBillLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Snapshot del código y porcentaje de IVA por línea de factura.
            // Elimina la inferencia global del ratio vatTotal/subtotal.
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "vat_code",       table: "sales_bill_line");
            migrationBuilder.DropColumn(name: "vat_percentage", table: "sales_bill_line");
        }
    }
}
