using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyBankAccountIdToTransferDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "company_bank_account_id",
                table: "payment_transfer_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_transfer_details_company_bank_account_id",
                table: "payment_transfer_details",
                column: "company_bank_account_id");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_transfer_details_company_bank_accounts_company_bank~",
                table: "payment_transfer_details",
                column: "company_bank_account_id",
                principalTable: "company_bank_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_transfer_details_company_bank_accounts_company_bank~",
                table: "payment_transfer_details");

            migrationBuilder.DropIndex(
                name: "IX_payment_transfer_details_company_bank_account_id",
                table: "payment_transfer_details");

            migrationBuilder.DropColumn(
                name: "company_bank_account_id",
                table: "payment_transfer_details");
        }
    }
}
