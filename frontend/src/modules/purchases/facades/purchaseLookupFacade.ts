/**
 * purchaseLookupFacade — superficie pública read-only de compras para
 * consumidores externos (inventory: investigación de kardex; reportes:
 * reporte de compras por proveedor).
 *
 * Expone únicamente listado, detalle y reporte; nunca las mutaciones de
 * purchaseService (create/update/applyDiscount/...). Los módulos externos
 * deben importar desde aquí, nunca directamente de purchases/api/purchaseService.
 */

import { purchaseService } from "../api/purchaseService";
import type { PurchasesReportRowDto, PurchasesReportTotalsDto } from "../api/purchaseService";

export type { PurchasesReportRowDto, PurchasesReportTotalsDto };

export const purchaseLookupFacade = {
  list: purchaseService.list,
  getById: purchaseService.getById,
  supplierReport: purchaseService.supplierReport,
};
