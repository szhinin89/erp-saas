using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// ZH-PURCHASE-EXPENSE-EXCLUSIVITY-RESTORE-01 — restaura la exclusividad Compra↔Gasto por
    /// AccessKey (una misma factura SRI no puede estar activa como compra y como gasto a la vez).
    /// Es raw SQL (trigger cruzado entre dos tablas; EF no lo modela), creado en
    /// 20260908175033_ExpensesFromPurchaseReception y ajustado en
    /// 20260911023601_ReceptionReprocessAfterCancelStandard para ignorar documentos Cancelled
    /// (PurchaseStatus.Cancelled = 3, ExpenseStatus.Cancelled = 2). Se perdió al consolidar el
    /// historial (4cbc4b12, 2026-09-25): la nueva línea base sale del model snapshot, que no lo conoce.
    /// Se restaura la versión VIGENTE (la de ReceptionReprocessAfterCancelStandard) sin cambios de
    /// comportamiento: mismo candado consultivo por tenant+AccessKey, mismo SQLSTATE 23505 y mismo
    /// CONSTRAINT 'uq_purchase_expense_access_key' que traducen PurchaseDraftUseCases y
    /// ExpenseDocumentDraftUseCases a su mensaje de UX.
    /// Anti-regresión: ERP.Architecture.Tests/RawSqlDatabaseObjectsSurviveMigrationSquashTests.
    /// </remarks>
    public partial class RestorePurchaseExpenseAccessKeyExclusivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mismo modo de lock que toma CREATE TRIGGER, tomado ANTES del pre-check: ninguna
            // escritura concurrente puede colar un conflicto entre la verificación y la creación
            // de los triggers (la migración corre en una sola transacción).
            migrationBuilder.Sql(
                """
                LOCK TABLE purchase_invoices, expense_documents IN SHARE ROW EXCLUSIVE MODE;
                """
            );

            // Sin el trigger, una BD existente pudo registrar la misma factura como compra y como
            // gasto activos. Decidir cuál conservar es de negocio: la migración se detiene con el
            // detalle, nunca anula ni borra documentos.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    conflicts text;
                BEGIN
                    SELECT string_agg(
                               format('tenant=%s access_key=%s purchase_invoice=%s expense_document=%s',
                                      p.tenant_id, p.access_key, p.id, e.id),
                               '; ' ORDER BY p.tenant_id, p.access_key)
                      INTO conflicts
                      FROM purchase_invoices p
                      JOIN expense_documents e
                        ON e.tenant_id = p.tenant_id
                       AND e.access_key = p.access_key
                     WHERE p.access_key IS NOT NULL
                       AND p.status <> 3
                       AND e.status <> 2;

                    IF conflicts IS NOT NULL THEN
                        RAISE EXCEPTION 'uq_purchase_expense_access_key: existen facturas activas registradas a la vez como compra y como gasto; anular una de cada par antes de migrar: %', conflicts;
                    END IF;
                END
                $$;
                """
            );

            migrationBuilder.Sql(
                """
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
                """
            );

            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS tr_expense_purchase_exclusivity ON expense_documents;
                CREATE TRIGGER tr_expense_purchase_exclusivity
                    BEFORE INSERT OR UPDATE OF tenant_id, access_key ON expense_documents
                    FOR EACH ROW EXECUTE FUNCTION enforce_purchase_expense_exclusivity();
                DROP TRIGGER IF EXISTS tr_purchase_expense_exclusivity ON purchase_invoices;
                CREATE TRIGGER tr_purchase_expense_exclusivity
                    BEFORE INSERT OR UPDATE OF tenant_id, access_key ON purchase_invoices
                    FOR EACH ROW EXECUTE FUNCTION enforce_purchase_expense_exclusivity();
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS tr_expense_purchase_exclusivity ON expense_documents;
                DROP TRIGGER IF EXISTS tr_purchase_expense_exclusivity ON purchase_invoices;
                DROP FUNCTION IF EXISTS enforce_purchase_expense_exclusivity();
                """
            );
        }
    }
}
