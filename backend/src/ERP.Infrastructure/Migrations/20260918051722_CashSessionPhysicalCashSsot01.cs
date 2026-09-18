using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CashSessionPhysicalCashSsot01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "affects_physical_cash",
                table: "payment_methods",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // CASH-SESSION-PHYSICAL-CASH-SSOT-01 — backfill de tenants ya existentes: sus filas
            // "EFECTIVO" fueron sembradas antes de esta columna y quedarían en false (default) sin
            // este UPDATE, lo que dejaría de registrar CashSession.SaleIncome incluso para ventas en
            // efectivo. Mismo patrón que PaymentMethodSriMappingBackfillService: idempotente, solo
            // toca el código conocido, nunca reasigna entre tenants.
            migrationBuilder.Sql(
                "UPDATE payment_methods SET affects_physical_cash = true WHERE code = 'EFECTIVO';"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "affects_physical_cash",
                table: "payment_methods");
        }
    }
}
