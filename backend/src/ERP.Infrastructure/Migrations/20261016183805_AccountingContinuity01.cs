using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccountingContinuity01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accounting_posting_mode",
                table: "document_flow_policy");

            migrationBuilder.AlterColumn<Guid>(
                name: "snapshot_accounting_account_id",
                table: "expense_lines",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "accounting_operational_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_operational_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounting_postings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    fact_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payload_version = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_postings", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounting_postings_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounting_posting_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounting_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    triggered_by = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_posting_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounting_posting_attempts_accounting_postings_accounting_~",
                        column: x => x.accounting_posting_id,
                        principalTable: "accounting_postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounting_operational_policies_scope",
                table: "accounting_operational_policies",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_accounting_operational_policies_company_from",
                table: "accounting_operational_policies",
                columns: new[] { "company_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounting_posting_attempts_scope",
                table: "accounting_posting_attempts",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_accounting_posting_attempts_posting_number",
                table: "accounting_posting_attempts",
                columns: new[] { "accounting_posting_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_postings_journal_entry_id",
                table: "accounting_postings",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounting_postings_scope_status_entry_date",
                table: "accounting_postings",
                columns: new[] { "tenant_id", "company_id", "status", "entry_date" });

            migrationBuilder.CreateIndex(
                name: "uq_accounting_postings_company_source_event_fact",
                table: "accounting_postings",
                columns: new[] { "company_id", "source_module", "source_event_id", "fact_type" },
                unique: true);

            // ACCOUNTING-CONTINUITY-01: toda empresa existente conserva su comportamiento previo
            // (contabilización de todos sus hechos) — versión Enabled desde 1900-01-01 (sin límite práctico). No se
            // fabrican obligaciones históricas: los documentos existentes ya tienen su asiento.
            // Empresas creadas después deben elegir su modo explícitamente.
            migrationBuilder.Sql(
                """
                INSERT INTO accounting_operational_policies
                    (id, tenant_id, company_id, mode, effective_from, effective_to, reason, created_at, created_by)
                SELECT gen_random_uuid(), c.tenant_id, c.id, 'Enabled', DATE '1900-01-01', NULL,
                       'ACCOUNTING-CONTINUITY-01: empresa existente migrada a Enabled.',
                       now(), '00000000-0000-0000-0000-000000000000'
                FROM company c;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_operational_policies");

            migrationBuilder.DropTable(
                name: "accounting_posting_attempts");

            migrationBuilder.DropTable(
                name: "accounting_postings");

            migrationBuilder.AlterColumn<Guid>(
                name: "snapshot_accounting_account_id",
                table: "expense_lines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "accounting_posting_mode",
                table: "document_flow_policy",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }
    }
}
