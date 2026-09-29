/**
 * purchaseReceptionExpenseFacade — operación pública cross-module de Purchases para Expenses:
 * preparar la precarga de un Nuevo Gasto desde un documento de recepción electrónica.
 *
 * `prepareExpenseDraft` delega en `purchaseReceptionService.createExpenseDraft`
 * (`POST /purchases/reception/{id}/create-expense-draft`, EXPENSES-FROM-RECEPTION-01). NO es un
 * lookup puro: valida elegibilidad (factura Verified con XML, sin compra/gasto previo con la misma
 * clave de acceso, proveedor activo) y, si el documento de recepción aún no tenía proveedor
 * vinculado, lo vincula y lo persiste en Purchases. Nunca crea ni persiste el ExpenseDocument —
 * eso ocurre al guardar desde el formulario de Gastos. Los módulos externos deben importar desde
 * aquí, nunca directamente de purchases/api/purchaseReceptionService.
 */
import { purchaseReceptionService } from "../api/purchaseReceptionService";
import type { ExpenseReceptionDraft } from "../api/purchaseReceptionService";

export type { ExpenseReceptionDraft };

export const purchaseReceptionExpenseFacade = {
  prepareExpenseDraft: purchaseReceptionService.createExpenseDraft,
};
