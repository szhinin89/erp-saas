using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesCollectionAccountSsotCleanup01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01 — payment_method_accounts solo debe tener
            // filas para métodos de pago Tarjeta/Cheque (PaymentMethod.DetailType). Efectivo
            // resuelve su cuenta exclusivamente desde CashRegister.AccountingAccountId,
            // Transferencia desde CompanyBankAccount.AccountingAccountId, y Crédito desde la regla
            // contable de Cuentas por Cobrar — ninguno de los tres debe tener (ni consultar) una
            // fila aquí. No hay columnas muertas que retirar: la tabla ya solo tiene
            // payment_method_id/accounting_account_id — el problema era de datos, no de esquema.
            migrationBuilder.Sql(
                """
                DELETE FROM payment_method_accounts pma
                USING payment_methods pm
                WHERE pma.payment_method_id = pm.id
                  AND (pm.is_credit_allowed = TRUE OR pm.detail_type NOT IN ('Card', 'Check'));
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Limpieza de datos legacy, no reversible: las filas de Efectivo/Transferencia/Crédito
            // borradas en Up() eran configuración incorrecta (segunda fuente de verdad duplicada) —
            // no existe un valor correcto al que "restaurarlas". Un rollback de esta migración deja
            // el esquema intacto (no se tocó ninguna columna/constraint); solo no recupera los datos.
        }
    }
}
