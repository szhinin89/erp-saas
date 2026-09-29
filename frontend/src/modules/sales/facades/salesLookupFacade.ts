/**
 * salesLookupFacade — superficie pública read-only de ventas para
 * consumidores externos (inventory: investigación de kardex; reportes:
 * reporte diario de ventas).
 *
 * Expone únicamente listado, detalle y reporte; nunca las mutaciones de
 * salesService (create/update/...). Los módulos externos deben importar desde
 * aquí, nunca directamente de sales/api/salesService.
 */

import { salesService } from "../api/salesService";
import type { SalesReportRowDto, SalesReportTotalsDto } from "../api/salesService";

export type { SalesReportRowDto, SalesReportTotalsDto };

export const salesLookupFacade = {
  list: salesService.list,
  getById: salesService.getById,
  dailyReport: salesService.dailyReport,
};
