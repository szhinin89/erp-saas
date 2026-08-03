using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyFinancialDestinationAuditValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "new_accounting_account_id",
                table: "company_financial_destination_audit",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "new_is_active",
                table: "company_financial_destination_audit",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "new_name",
                table: "company_financial_destination_audit",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "old_accounting_account_id",
                table: "company_financial_destination_audit",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "old_is_active",
                table: "company_financial_destination_audit",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "old_name",
                table: "company_financial_destination_audit",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "new_accounting_account_id",
                table: "company_financial_destination_audit");

            migrationBuilder.DropColumn(
                name: "new_is_active",
                table: "company_financial_destination_audit");

            migrationBuilder.DropColumn(
                name: "new_name",
                table: "company_financial_destination_audit");

            migrationBuilder.DropColumn(
                name: "old_accounting_account_id",
                table: "company_financial_destination_audit");

            migrationBuilder.DropColumn(
                name: "old_is_active",
                table: "company_financial_destination_audit");

            migrationBuilder.DropColumn(
                name: "old_name",
                table: "company_financial_destination_audit");
        }
    }
}
