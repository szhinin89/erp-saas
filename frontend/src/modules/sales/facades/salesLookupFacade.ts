/**
 * salesLookupFacade — superficie pública read-only de ventas para
 * consumidores externos (inventory, investigación de kardex).
 *
 * Expone únicamente listado y detalle; nunca las mutaciones de salesService
 * (create/update/...). Los módulos externos deben importar desde aquí,
 * nunca directamente de sales/api/salesService.
 */

import { salesService } from "../api/salesService";

export const salesLookupFacade = {
  list: salesService.list,
  getById: salesService.getById,
};
