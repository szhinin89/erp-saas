/**
 * priceListLookupFacade — superficie pública read-only de listas de precios
 * para consumidores externos (formularios de items).
 *
 * Expone únicamente el listado; nunca las mutaciones de priceListService
 * (create/update/enable/disable). Los módulos externos deben importar desde
 * aquí, nunca directamente de pricing/api/pricingService.
 */

import { priceListService } from "../api/pricingService";
import type { PriceListDto, PriceSource } from "../api/pricingService";

export type { PriceListDto, PriceSource };

export const priceListLookupFacade = {
  list: priceListService.list,
};
