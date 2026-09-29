/**
 * stockLookupFacade — superficie pública read-only de existencias para consumidores
 * externos (sales: disponibilidad por bodega al facturar; reportes: reporte de stock).
 *
 * Expone únicamente consultas; nunca movimientos, ajustes ni transferencias. Los módulos
 * externos deben importar desde aquí, nunca directamente de
 * inventory/stock/api/stockService.
 */

import { stockService } from "../stock/api/stockService";
import type {
  ItemWarehouseAvailabilityDto,
  StockReportRowDto,
  StockReportStatus,
} from "../stock/api/stockService";

export type { ItemWarehouseAvailabilityDto, StockReportRowDto, StockReportStatus };

export const stockLookupFacade = {
  getWarehouseAvailability: stockService.getWarehouseAvailability,
  getReport: stockService.getReport,
};
