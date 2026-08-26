using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentFinancialDestinationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "financial_destination_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_financial_destination_id",
                table: "payments",
                column: "financial_destination_id");

            migrationBuilder.AddForeignKey(
                name: "FK_payments_company_financial_destinations_financial_destinati~",
                table: "payments",
                column: "financial_destination_id",
                principalTable: "company_financial_destinations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payments_company_financial_destinations_financial_destinati~",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_financial_destination_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "financial_destination_id",
                table: "payments");
        }
    }
}
