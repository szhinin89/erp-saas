using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialDestinationToBankAccountMigration01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01 — fase 1 (aditiva): agrega las
            // columnas/FKs nuevas de CompanyBankAccount/CashRegister sin tocar todavía
            // financial_destination_id ni company_financial_destinations — eso se retira en una
            // migración de limpieza posterior, solo después de correr el backfill (regla del
            // ticket: no borrar histórico sin migración comprobada).
            //
            // payments.financial_destination_id se renombra abajo a company_bank_account_id — su
            // FK vieja hacia company_financial_destinations queda apuntando a la columna renombrada
            // y debe soltarse ya (si no, cualquier company_bank_accounts.Id real que no exista
            // también en company_financial_destinations rompería el insert).
            migrationBuilder.DropForeignKey(
                name: "FK_payments_company_financial_destinations_financial_destinati~",
                table: "payments");

            migrationBuilder.RenameColumn(
                name: "financial_destination_name_snapshot",
                table: "supplier_credit_refund_transactions",
                newName: "destination_type_snapshot");

            migrationBuilder.RenameColumn(
                name: "financial_destination_code_snapshot",
                table: "supplier_credit_refund_transactions",
                newName: "destination_name_snapshot");

            migrationBuilder.RenameColumn(
                name: "destination_type_code_snapshot",
                table: "supplier_credit_refund_transactions",
                newName: "destination_code_snapshot");

            migrationBuilder.RenameColumn(
                name: "financial_destination_id",
                table: "supplier_credit_audit",
                newName: "company_bank_account_id");

            migrationBuilder.RenameColumn(
                name: "financial_destination_code_snapshot",
                table: "supplier_credit_audit",
                newName: "destination_code_snapshot");

            migrationBuilder.RenameColumn(
                name: "destination_type_code_snapshot",
                table: "supplier_credit_audit",
                newName: "destination_type_snapshot");

            migrationBuilder.RenameColumn(
                name: "financial_destination_id",
                table: "payments",
                newName: "company_bank_account_id");

            migrationBuilder.RenameIndex(
                name: "IX_payments_financial_destination_id",
                table: "payments",
                newName: "IX_payments_company_bank_account_id");

            migrationBuilder.AddColumn<Guid>(
                name: "cash_register_id",
                table: "supplier_payment_methods",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "company_bank_account_id",
                table: "supplier_payment_methods",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cash_register_id",
                table: "supplier_credit_refund_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "company_bank_account_id",
                table: "supplier_credit_refund_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cash_register_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "accounting_account_id",
                table: "cash_registers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_methods_bank_account",
                table: "supplier_payment_methods",
                column: "company_bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_methods_cash_register",
                table: "supplier_payment_methods",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_cash_register_id",
                table: "supplier_credit_refund_transactions",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_company_bank_account_id",
                table: "supplier_credit_refund_transactions",
                column: "company_bank_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_cash_register_id",
                table: "payments",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_accounting_account_id",
                table: "cash_registers",
                column: "accounting_account_id");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_registers_accounts_accounting_account_id",
                table: "cash_registers",
                column: "accounting_account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_cash_registers_cash_register_id",
                table: "payments",
                column: "cash_register_id",
                principalTable: "cash_registers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_company_bank_accounts_company_bank_account_id",
                table: "payments",
                column: "company_bank_account_id",
                principalTable: "company_bank_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_credit_refund_transactions_cash_registers_cash_reg~",
                table: "supplier_credit_refund_transactions",
                column: "cash_register_id",
                principalTable: "cash_registers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_credit_refund_transactions_company_bank_accounts_c~",
                table: "supplier_credit_refund_transactions",
                column: "company_bank_account_id",
                principalTable: "company_bank_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_payment_methods_cash_registers_cash_register_id",
                table: "supplier_payment_methods",
                column: "cash_register_id",
                principalTable: "cash_registers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_payment_methods_company_bank_accounts_company_bank~",
                table: "supplier_payment_methods",
                column: "company_bank_account_id",
                principalTable: "company_bank_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_registers_accounts_accounting_account_id",
                table: "cash_registers");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_cash_registers_cash_register_id",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_company_bank_accounts_company_bank_account_id",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_credit_refund_transactions_cash_registers_cash_reg~",
                table: "supplier_credit_refund_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_credit_refund_transactions_company_bank_accounts_c~",
                table: "supplier_credit_refund_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_payment_methods_cash_registers_cash_register_id",
                table: "supplier_payment_methods");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_payment_methods_company_bank_accounts_company_bank~",
                table: "supplier_payment_methods");

            migrationBuilder.DropIndex(
                name: "ix_supplier_payment_methods_bank_account",
                table: "supplier_payment_methods");

            migrationBuilder.DropIndex(
                name: "ix_supplier_payment_methods_cash_register",
                table: "supplier_payment_methods");

            migrationBuilder.DropIndex(
                name: "IX_supplier_credit_refund_transactions_cash_register_id",
                table: "supplier_credit_refund_transactions");

            migrationBuilder.DropIndex(
                name: "IX_supplier_credit_refund_transactions_company_bank_account_id",
                table: "supplier_credit_refund_transactions");

            migrationBuilder.DropIndex(
                name: "IX_payments_cash_register_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_cash_registers_accounting_account_id",
                table: "cash_registers");

            migrationBuilder.DropColumn(
                name: "cash_register_id",
                table: "supplier_payment_methods");

            migrationBuilder.DropColumn(
                name: "company_bank_account_id",
                table: "supplier_payment_methods");

            migrationBuilder.DropColumn(
                name: "cash_register_id",
                table: "supplier_credit_refund_transactions");

            migrationBuilder.DropColumn(
                name: "company_bank_account_id",
                table: "supplier_credit_refund_transactions");

            migrationBuilder.DropColumn(
                name: "cash_register_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "accounting_account_id",
                table: "cash_registers");

            migrationBuilder.RenameColumn(
                name: "destination_type_snapshot",
                table: "supplier_credit_refund_transactions",
                newName: "financial_destination_name_snapshot");

            migrationBuilder.RenameColumn(
                name: "destination_name_snapshot",
                table: "supplier_credit_refund_transactions",
                newName: "financial_destination_code_snapshot");

            migrationBuilder.RenameColumn(
                name: "destination_code_snapshot",
                table: "supplier_credit_refund_transactions",
                newName: "destination_type_code_snapshot");

            migrationBuilder.RenameColumn(
                name: "destination_type_snapshot",
                table: "supplier_credit_audit",
                newName: "destination_type_code_snapshot");

            migrationBuilder.RenameColumn(
                name: "destination_code_snapshot",
                table: "supplier_credit_audit",
                newName: "financial_destination_code_snapshot");

            migrationBuilder.RenameColumn(
                name: "company_bank_account_id",
                table: "supplier_credit_audit",
                newName: "financial_destination_id");

            migrationBuilder.RenameColumn(
                name: "company_bank_account_id",
                table: "payments",
                newName: "financial_destination_id");

            migrationBuilder.RenameIndex(
                name: "IX_payments_company_bank_account_id",
                table: "payments",
                newName: "IX_payments_financial_destination_id");

            migrationBuilder.AddForeignKey(
                name: "FK_payments_company_financial_destinations_financial_destinati~",
                table: "payments",
                column: "financial_destination_id",
                principalTable: "company_financial_destinations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
