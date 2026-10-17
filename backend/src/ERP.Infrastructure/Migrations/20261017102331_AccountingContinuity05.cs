using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccountingContinuity05 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_document_id",
                table: "accounting_postings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounting_postings_scope_source_document",
                table: "accounting_postings",
                columns: new[] { "tenant_id", "company_id", "source_document_id" },
                filter: "source_document_id IS NOT NULL");

            // ACCOUNTING-CONTINUITY-05 — migra las filas existentes de inventory_cost_postings a la
            // obligación común preservando identidad (company, Sales, source_event_id, fact_type),
            // estado y asiento. BusinessDate = emisión de la factura (ancla de aplicabilidad). Única
            // excepción: filas de importe cero sin asiento (cualquier estado), sin efecto contable que
            // preservar; permanecen solo como evidencia legacy de solo lectura.
            // No se fabrican obligaciones para hechos sin fila. La tabla legacy queda sin escrituras.
            migrationBuilder.Sql(
                """
                INSERT INTO accounting_postings (
                    id, tenant_id, company_id, source_module, fact_type, source_event_id,
                    source_document_number, business_date, entry_date, payload_version, payload,
                    status, error_code, error_message, attempts, last_attempt_at_utc,
                    journal_entry_id, correlation_id, source_document_id,
                    created_at, updated_at, created_by, updated_by)
                SELECT p."Id", p."TenantId", p."CompanyId", 'Sales', p."FactType", p."SourceEventId",
                    si.invoice_number, COALESCE(si.issue_date, p."EntryDate"), p."EntryDate", 2,
                    jsonb_build_object(
                        'subtotal', 0, 'totalVat', 0, 'totalIce', 0, 'totalDiscount', 0, 'grandTotal', 0,
                        'historicalCostTotal', p."Amount",
                        'references', jsonb_build_object('SalesInvoice', p."InvoiceId")),
                    p."Status", p."ErrorCode", p."ErrorMessage", p."Attempts", NULL,
                    p."JournalEntryId", NULL, p."InvoiceId",
                    p."CreatedAt", p."UpdatedAt", p."CreatedBy", p."UpdatedBy"
                FROM inventory_cost_postings p
                LEFT JOIN sales_invoices si
                    ON si.id = p."InvoiceId" AND si.tenant_id = p."TenantId" AND si.company_id = p."CompanyId"
                WHERE NOT (p."Amount" = 0 AND p."JournalEntryId" IS NULL)
                ON CONFLICT (company_id, source_module, source_event_id, fact_type) DO NOTHING;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM accounting_posting_attempts a USING inventory_cost_postings p
                    WHERE a.accounting_posting_id = p."Id";
                DELETE FROM accounting_postings ap USING inventory_cost_postings p WHERE ap.id = p."Id";
                """
            );

            migrationBuilder.DropIndex(
                name: "ix_accounting_postings_scope_source_document",
                table: "accounting_postings");

            migrationBuilder.DropColumn(
                name: "source_document_id",
                table: "accounting_postings");
        }
    }
}
