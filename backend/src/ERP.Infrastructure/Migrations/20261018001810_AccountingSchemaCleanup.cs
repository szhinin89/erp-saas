using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccountingSchemaCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ACCOUNTING-SCHEMA-CLEANUP — fail-closed antes de retirar el esquema heredado:
            // (1) cada fila de inventory_cost_postings con efecto (importe o asiento) ya vive en la
            // obligación común (AccountingContinuity05); solo se descartan las de importe cero sin asiento.
            // (2) cada asiento heredado Reversed (status = 2) tiene su reverso enlazado por
            // original_journal_entry_id; queda Posted, el estado vigente de un original reversado
            // (sus líneas, número y fechas no cambian). Sin esas condiciones la migración aborta.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM inventory_cost_postings p
                        WHERE NOT (p."Amount" = 0 AND p."JournalEntryId" IS NULL)
                          AND NOT EXISTS (
                              SELECT 1 FROM accounting_postings ap
                              WHERE ap.company_id = p."CompanyId" AND ap.source_module = 'Sales'
                                AND ap.source_event_id = p."SourceEventId" AND ap.fact_type = p."FactType")
                    ) THEN
                        RAISE EXCEPTION 'ACCOUNTING-SCHEMA-CLEANUP: inventory_cost_postings tiene filas con efecto sin obligación común.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM journal_entries o
                        WHERE o.status = 2
                          AND NOT EXISTS (SELECT 1 FROM journal_entries r WHERE r.original_journal_entry_id = o.id)
                    ) THEN
                        RAISE EXCEPTION 'ACCOUNTING-SCHEMA-CLEANUP: asiento Reversed sin reverso enlazado por original_journal_entry_id.';
                    END IF;
                END $$;
                UPDATE journal_entries SET status = 1 WHERE status = 2;
                """
            );

            migrationBuilder.DropForeignKey(
                name: "FK_journal_entries_journal_entries_reverse_journal_entry_id",
                table: "journal_entries");

            migrationBuilder.DropTable(
                name: "inventory_cost_postings");

            migrationBuilder.DropIndex(
                name: "IX_journal_entries_reverse_journal_entry_id",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "reverse_journal_entry_id",
                table: "journal_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo restaura el esquema (vacío): los datos heredados no se reconstruyen.
            migrationBuilder.AddColumn<Guid>(
                name: "reverse_journal_entry_id",
                table: "journal_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "inventory_cost_postings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(22,10)", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FactType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_cost_postings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_reverse_journal_entry_id",
                table: "journal_entries",
                column: "reverse_journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_cost_postings_CompanyId_SourceEventId_FactType",
                table: "inventory_cost_postings",
                columns: new[] { "CompanyId", "SourceEventId", "FactType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_cost_postings_TenantId_CompanyId_InvoiceId",
                table: "inventory_cost_postings",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entries_journal_entries_reverse_journal_entry_id",
                table: "journal_entries",
                column: "reverse_journal_entry_id",
                principalTable: "journal_entries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
