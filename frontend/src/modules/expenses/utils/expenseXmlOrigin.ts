import type { ExpenseReceptionDraft } from "../../purchases/facades/purchaseReceptionExpenseFacade";
import type { ExpenseDocumentHeaderState } from "../components/ExpenseDocumentHeader";

/**
 * EXPENSE-UI-02 — solo presentación: qué campos de cabecera traen valor del comprobante XML
 * recibido. No altera la importación ni los valores (siguen siendo los reales del XML).
 */
export function receptionXmlHeaderFields(
  reception: ExpenseReceptionDraft | null,
): ReadonlySet<keyof ExpenseDocumentHeaderState> {
  const fields = new Set<keyof ExpenseDocumentHeaderState>();
  if (!reception) return fields;
  if (reception.supplierId) fields.add("supplierId");
  if (reception.issueDate) fields.add("issueDate");
  if (reception.documentType) fields.add("documentType");
  if (reception.documentNumber) fields.add("documentNumber");
  if (reception.authorizationNumber) fields.add("authorizationNumber");
  if (reception.authorizationDate) fields.add("authorizationDate");
  return fields;
}
