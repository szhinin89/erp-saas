using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupplierPaymentCashTransferHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cash_movement_id",
                table: "supplier_payment_methods",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cash_session_id",
                table: "supplier_payment_methods",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "transaction_date",
                table: "supplier_payment_methods",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_methods_bank_reconciliation",
                table: "supplier_payment_methods",
                columns: new[] { "tenant_id", "company_bank_account_id", "transaction_date", "reference_number" },
                filter: "company_bank_account_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_methods_cash_session",
                table: "supplier_payment_methods",
                column: "cash_session_id");

            migrationBuilder.CreateIndex(
                name: "ux_supplier_payment_methods_cash_movement",
                table: "supplier_payment_methods",
                column: "cash_movement_id",
                unique: true,
                filter: "cash_movement_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "chk_supplier_payment_methods_bank_transaction_date",
                table: "supplier_payment_methods",
                sql: "\"company_bank_account_id\" IS NULL OR \"transaction_date\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_payment_methods_cash_movements_cash_movement_id",
                table: "supplier_payment_methods",
                column: "cash_movement_id",
                principalTable: "cash_movements",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_payment_methods_cash_sessions_cash_session_id",
                table: "supplier_payment_methods",
                column: "cash_session_id",
                principalTable: "cash_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_supplier_payment_methods_cash_movements_cash_movement_id",
                table: "supplier_payment_methods");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_payment_methods_cash_sessions_cash_session_id",
                table: "supplier_payment_methods");

            migrationBuilder.DropIndex(
                name: "ix_supplier_payment_methods_bank_reconciliation",
                table: "supplier_payment_methods");

            migrationBuilder.DropIndex(
                name: "ix_supplier_payment_methods_cash_session",
                table: "supplier_payment_methods");

            migrationBuilder.DropIndex(
                name: "ux_supplier_payment_methods_cash_movement",
                table: "supplier_payment_methods");

            migrationBuilder.DropCheckConstraint(
                name: "chk_supplier_payment_methods_bank_transaction_date",
                table: "supplier_payment_methods");

            migrationBuilder.DropColumn(
                name: "cash_movement_id",
                table: "supplier_payment_methods");

            migrationBuilder.DropColumn(
                name: "cash_session_id",
                table: "supplier_payment_methods");

            migrationBuilder.DropColumn(
                name: "transaction_date",
                table: "supplier_payment_methods");
        }
    }
}
