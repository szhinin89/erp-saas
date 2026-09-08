using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpensesFromPurchaseReception : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "access_key",
                table: "expense_documents",
                type: "character varying(49)",
                maxLength: 49,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reception_document_id",
                table: "expense_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_expense_documents_reception_document_id",
                table: "expense_documents",
                column: "reception_document_id");

            migrationBuilder.CreateIndex(
                name: "uq_expense_documents_tenant_access_key",
                table: "expense_documents",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "\"access_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_expense_documents_tenant_reception_document_id",
                table: "expense_documents",
                columns: new[] { "tenant_id", "reception_document_id" },
                unique: true,
                filter: "\"reception_document_id\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_expense_documents_purchase_reception_documents_reception_do~",
                table: "expense_documents",
                column: "reception_document_id",
                principalTable: "purchase_reception_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            // The two aggregates share a fiscal identity. Serialize competing writes before
            // checking the opposite table; the lock is held until transaction completion.
            migrationBuilder.Sql("""
                CREATE FUNCTION enforce_purchase_expense_exclusivity() RETURNS trigger
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
                CREATE TRIGGER tr_expense_purchase_exclusivity
                    BEFORE INSERT OR UPDATE OF tenant_id, access_key ON expense_documents
                    FOR EACH ROW EXECUTE FUNCTION enforce_purchase_expense_exclusivity();
                CREATE TRIGGER tr_purchase_expense_exclusivity
                    BEFORE INSERT OR UPDATE OF tenant_id, access_key ON purchase_invoices
                    FOR EACH ROW EXECUTE FUNCTION enforce_purchase_expense_exclusivity();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER tr_expense_purchase_exclusivity ON expense_documents;
                DROP TRIGGER tr_purchase_expense_exclusivity ON purchase_invoices;
                DROP FUNCTION enforce_purchase_expense_exclusivity();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_expense_documents_purchase_reception_documents_reception_do~",
                table: "expense_documents");

            migrationBuilder.DropIndex(
                name: "IX_expense_documents_reception_document_id",
                table: "expense_documents");

            migrationBuilder.DropIndex(
                name: "uq_expense_documents_tenant_access_key",
                table: "expense_documents");

            migrationBuilder.DropIndex(
                name: "uq_expense_documents_tenant_reception_document_id",
                table: "expense_documents");

            migrationBuilder.DropColumn(
                name: "access_key",
                table: "expense_documents");

            migrationBuilder.DropColumn(
                name: "reception_document_id",
                table: "expense_documents");
        }
    }
}
