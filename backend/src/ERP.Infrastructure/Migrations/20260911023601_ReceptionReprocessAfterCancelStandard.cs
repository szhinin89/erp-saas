using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReceptionReprocessAfterCancelStandard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_purchase_invoices_tenant_access_key",
                table: "purchase_invoices");

            migrationBuilder.DropIndex(
                name: "uq_purchase_invoices_tenant_company_supplier_number",
                table: "purchase_invoices");

            migrationBuilder.DropIndex(
                name: "uq_expense_documents_tenant_access_key",
                table: "expense_documents");

            migrationBuilder.DropIndex(
                name: "uq_expense_documents_tenant_company_supplier_type_number",
                table: "expense_documents");

            migrationBuilder.DropIndex(
                name: "uq_expense_documents_tenant_reception_document_id",
                table: "expense_documents");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_invoices_tenant_access_key",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL AND status <> 3");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_invoices_tenant_company_supplier_number",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "invoice_number" },
                unique: true,
                filter: "\"status\" <> 3");

            migrationBuilder.CreateIndex(
                name: "uq_expense_documents_tenant_access_key",
                table: "expense_documents",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "\"access_key\" IS NOT NULL AND \"status\" <> 2");

            migrationBuilder.CreateIndex(
                name: "uq_expense_documents_tenant_company_supplier_type_number",
                table: "expense_documents",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "document_type", "document_number" },
                unique: true,
                filter: "\"status\" <> 2");

            migrationBuilder.CreateIndex(
                name: "uq_expense_documents_tenant_reception_document_id",
                table: "expense_documents",
                columns: new[] { "tenant_id", "reception_document_id" },
                unique: true,
                filter: "\"reception_document_id\" IS NOT NULL AND \"status\" <> 2");

            // RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — el trigger cruzado
            // enforce_purchase_expense_exclusivity() (ExpensesFromPurchaseReception) serializa
            // Compra↔Gasto por AccessKey pero nunca consideró Status: una compra/gasto Cancelled
            // en la tabla opuesta seguía bloqueando para siempre el mismo AccessKey en la otra,
            // aun después de que los índices únicos de arriba ya lo permiten. CREATE OR REPLACE
            // agrega "AND status <> 3/2" (Cancelled) a cada EXISTS — mismo criterio que el resto
            // de esta migración, nunca cambia el resto del comportamiento (candado consultivo,
            // mensaje de error, nombre de función/triggers).
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
                                AND status <> 3) THEN
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION enforce_purchase_expense_exclusivity() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.access_key IS NULL THEN RETURN NEW; END IF;
                    PERFORM pg_advisory_xact_lock(hashtextextended(
                        'purchase-expense:' || NEW.tenant_id::text || ':' || NEW.access_key, 0));
                    IF TG_TABLE_NAME = 'expense_documents' THEN
                        IF EXISTS (SELECT 1 FROM purchase_invoices
                            WHERE tenant_id = NEW.tenant_id AND access_key = NEW.access_key) THEN
                            RAISE EXCEPTION 'La factura ya fue registrada como compra.'
                                USING ERRCODE = '23505', CONSTRAINT = 'uq_purchase_expense_access_key';
                        END IF;
                    ELSE
                        IF EXISTS (SELECT 1 FROM expense_documents
                            WHERE tenant_id = NEW.tenant_id AND access_key = NEW.access_key) THEN
                            RAISE EXCEPTION 'La factura ya fue registrada como gasto.'
                                USING ERRCODE = '23505', CONSTRAINT = 'uq_purchase_expense_access_key';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "uq_purchase_invoices_tenant_access_key",
                table: "purchase_invoices");

            migrationBuilder.DropIndex(
                name: "uq_purchase_invoices_tenant_company_supplier_number",
                table: "purchase_invoices");

            migrationBuilder.DropIndex(
                name: "uq_expense_documents_tenant_access_key",
                table: "expense_documents");

            migrationBuilder.DropIndex(
                name: "uq_expense_documents_tenant_company_supplier_type_number",
                table: "expense_documents");

            migrationBuilder.DropIndex(
                name: "uq_expense_documents_tenant_reception_document_id",
                table: "expense_documents");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_invoices_tenant_access_key",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_invoices_tenant_company_supplier_number",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_expense_documents_tenant_access_key",
                table: "expense_documents",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "\"access_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_expense_documents_tenant_company_supplier_type_number",
                table: "expense_documents",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "document_type", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_expense_documents_tenant_reception_document_id",
                table: "expense_documents",
                columns: new[] { "tenant_id", "reception_document_id" },
                unique: true,
                filter: "\"reception_document_id\" IS NOT NULL");
        }
    }
}
