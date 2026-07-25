using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingCoreFoundations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounting_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    period_number = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    closed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_periods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    parent_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    account_type = table.Column<int>(type: "integer", nullable: false),
                    nature = table.Column<int>(type: "integer", nullable: false),
                    allows_posting = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "posting_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fact_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    debit_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credit_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posting_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    accounting_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_journal_entries_accounting_periods_accounting_period_id",
                        column: x => x.accounting_period_id,
                        principalTable: "accounting_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_accounting_periods_company_year_period",
                table: "accounting_periods",
                columns: new[] { "company_id", "fiscal_year", "period_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_company_parent",
                table: "accounts",
                columns: new[] { "company_id", "parent_account_id" });

            migrationBuilder.CreateIndex(
                name: "uq_accounts_company_code",
                table: "accounts",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_accounting_period_id",
                table: "journal_entries",
                column: "accounting_period_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_company_source_event",
                table: "journal_entries",
                columns: new[] { "company_id", "source_event_id" });

            migrationBuilder.CreateIndex(
                name: "uq_posting_rules_company_source_fact",
                table: "posting_rules",
                columns: new[] { "company_id", "source_module", "fact_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropTable(
                name: "posting_rules");

            migrationBuilder.DropTable(
                name: "accounting_periods");
        }
    }
}
