using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CashMovementReasonAccount12C : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "accounting_account_id",
                table: "cash_movement_reasons",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_movement_reasons_accounting_account_id",
                table: "cash_movement_reasons",
                column: "accounting_account_id");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_movement_reasons_accounts_accounting_account_id",
                table: "cash_movement_reasons",
                column: "accounting_account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_movement_reasons_accounts_accounting_account_id",
                table: "cash_movement_reasons");

            migrationBuilder.DropIndex(
                name: "IX_cash_movement_reasons_accounting_account_id",
                table: "cash_movement_reasons");

            migrationBuilder.DropColumn(
                name: "accounting_account_id",
                table: "cash_movement_reasons");
        }
    }
}
