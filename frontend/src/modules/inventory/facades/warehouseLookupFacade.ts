/**
 * warehouseLookupFacade — superficie pública read-only de bodegas para
 * consumidores externos (branches, cashRegisters, purchases).
 *
 * Expone únicamente el listado; nunca las mutaciones de warehouseService
 * (create/update/disable/enable) ni el detalle. Los módulos externos deben
 * importar desde aquí, nunca directamente de
 * inventory/warehouses/api/warehouseService.
 */

import { warehouseService } from "../warehouses/api/warehouseService";
import type {
  WarehouseDto,
  CatalogActiveStatus,
} from "../warehouses/api/warehouseService";

export type { WarehouseDto, CatalogActiveStatus };

export const warehouseLookupFacade = {
  list: warehouseService.list,
};
