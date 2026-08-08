/**
 * purchaseLookupFacade — superficie pública read-only de compras para
 * consumidores externos (inventory, investigación de kardex).
 *
 * Expone únicamente listado y detalle; nunca las mutaciones de
 * purchaseService (create/update/applyDiscount/...). Los módulos externos
 * deben importar desde aquí, nunca directamente de purchases/api/purchaseService.
 */

import { purchaseService } from "../api/purchaseService";

export const purchaseLookupFacade = {
  list: purchaseService.list,
  getById: purchaseService.getById,
};
