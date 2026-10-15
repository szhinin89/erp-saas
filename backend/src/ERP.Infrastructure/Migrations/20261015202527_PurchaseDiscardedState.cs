using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseDiscardedState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Replace BOTH legacy indexes before converting historical rows to Discarded:
            // a later active purchase may already reuse either historical unique key.
            migrationBuilder.DropIndex(
                name: "uq_purchase_invoices_tenant_access_key",
                table: "purchase_invoices");

            migrationBuilder.DropIndex(
                name: "uq_purchase_invoices_tenant_company_supplier_number",
                table: "purchase_invoices");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_invoices_tenant_access_key",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL AND status IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_invoices_tenant_company_supplier_number",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "invoice_number" },
                unique: true,
                filter: "\"status\" IN (1, 2)");

            ReplaceExclusivityFunction(migrationBuilder);
            // One-time conversion; all constraints now recognize only Draft/Confirmed as active.
            migrationBuilder.Sql("UPDATE purchase_invoices SET status = 4 WHERE status = 3 AND confirmed_grand_total IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE purchase_invoices SET status = 3 WHERE status = 4;");
            ReplaceExclusivityFunction(migrationBuilder);
            migrationBuilder.DropIndex(
                name: "uq_purchase_invoices_tenant_access_key",
                table: "purchase_invoices");

            migrationBuilder.DropIndex(
                name: "uq_purchase_invoices_tenant_company_supplier_number",
                table: "purchase_invoices");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_invoices_tenant_access_key",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL AND status IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_invoices_tenant_company_supplier_number",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "invoice_number" },
                unique: true,
                filter: "\"status\" IN (1, 2)");
        }

        private static void ReplaceExclusivityFunction(MigrationBuilder migrationBuilder)
        {
            // Preserve the existing tenant-wide lock, scope and expense semantics.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION enforce_purchase_expense_exclusivity() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.access_key IS NULL THEN RETURN NEW; END IF;
                    PERFORM pg_advisory_xact_lock(hashtextextended(
                        'purchase-expense:' || NEW.tenant_id::text || ':' || NEW.access_key, 0));
                    IF TG_TABLE_NAME = 'expense_documents' THEN
                        IF EXISTS (SELECT 1 FROM purchase_invoices
                            WHERE tenant_id = NEW.tenant_id AND access_key = NEW.access_key
                                AND status IN (1, 2)) THEN
                            RAISE EXCEPTION 'La factura ya fue registrada como compra.'
                                USING ERRCODE = '23505', CONSTRAINT = 'uq_purchase_expense_access_key';
                        END IF;
                    ELSE
                        IF EXISTS (SELECT 1 FROM expense_documents
                            WHERE tenant_id = NEW.tenant_id AND access_key = NEW.access_key
                                AND status <> 2) THEN
                            RAISE EXCEPTION 'La factura ya fue registrada como gasto.'
                                USING ERRCODE = '23505', CONSTRAINT = 'uq_purchase_expense_access_key';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);
        }
    }
}
