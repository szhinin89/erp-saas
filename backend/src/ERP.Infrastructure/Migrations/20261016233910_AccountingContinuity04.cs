using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccountingContinuity04 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "accounting_account_id",
                table: "supplier_credit_refund_transactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "accounting_account_code_snapshot",
                table: "supplier_credit_refund_transactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volver a NOT NULL exige que ningún reembolso dependa de la cuenta opcional. Si existe
            // alguno sin cuenta, el rollback se aborta de forma explícita: no se inventa una cuenta
            // ni se borran datos.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM supplier_credit_refund_transactions
                        WHERE accounting_account_id IS NULL OR accounting_account_code_snapshot IS NULL
                    ) THEN
                        RAISE EXCEPTION 'AccountingContinuity04 Down abortado: existen supplier_credit_refund_transactions sin cuenta contable de referencia. Asigne cuenta y código a esas filas antes de revertir.';
                    END IF;
                END $$;
                """
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "accounting_account_id",
                table: "supplier_credit_refund_transactions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "accounting_account_code_snapshot",
                table: "supplier_credit_refund_transactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
