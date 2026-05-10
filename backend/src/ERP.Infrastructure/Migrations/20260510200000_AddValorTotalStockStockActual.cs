using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddValorTotalStockStockActual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "valor_total_stock",
                table: "stock_actual",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            // Inicializar valor_total_stock en 0 para registros existentes.
            // El kardex calculará el valor real a partir del historial de movimientos.
            // A medida que se registren nuevas compras, el valor se actualizará automáticamente.
            migrationBuilder.Sql(
                "UPDATE stock_actual SET valor_total_stock = 0 WHERE valor_total_stock IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "valor_total_stock", table: "stock_actual");
        }
    }
}
