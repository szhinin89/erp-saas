using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyIdToAccountingAndCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_accounting_periods_subscriber_year_month",
                table: "accounting_periods");

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "petty_cash_expense",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "petty_cash",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "journal_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "cash_count",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "bank_statement",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "bank_account",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "accounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "accounting_setup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "accounting_periods",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_subscriber_company",
                table: "journal_entries",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_subscriber_company",
                table: "accounts",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_accounting_setup_subscriber_company",
                table: "accounting_setup",
                columns: new[] { "subscriber_id", "company_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounting_periods_subscriber_company",
                table: "accounting_periods",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_accounting_periods_subscriber_company_year_month",
                table: "accounting_periods",
                columns: new[] { "subscriber_id", "company_id", "year", "month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_journal_entries_subscriber_company",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "ix_accounts_subscriber_company",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "uq_accounting_setup_subscriber_company",
                table: "accounting_setup");

            migrationBuilder.DropIndex(
                name: "ix_accounting_periods_subscriber_company",
                table: "accounting_periods");

            migrationBuilder.DropIndex(
                name: "uq_accounting_periods_subscriber_company_year_month",
                table: "accounting_periods");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "petty_cash_expense");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "petty_cash");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "cash_count");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "bank_statement");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "bank_account");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "accounting_setup");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "accounting_periods");

            migrationBuilder.CreateIndex(
                name: "uq_accounting_periods_subscriber_year_month",
                table: "accounting_periods",
                columns: new[] { "subscriber_id", "year", "month" },
                unique: true);
        }
    }
}
