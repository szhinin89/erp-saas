import { apiGet } from "../../lib/apiEnvelope";

const BASE = "/api/v1/sales";

export interface SalesItemPricingDto {
  itemId: string;
  unitPrice: number;
  vatCode: string | null;
  vatName: string | null;
  iceCode: string | null;
  iceName: string | null;
  maxDiscountPercent: number | null;
  priceListCode: string;
  /** SALES-PRICE-LIST-DISCOUNT-VISIBILITY-01: precio del ítem antes de la lista de precios —
   * junto con priceListName/discountDescription explican por qué unitPrice puede diferir del
   * precio base. discountDescription es null cuando unitPrice === basePrice (sin regla aplicada). */
  basePrice: number;
  priceListName: string;
  discountDescription: string | null;
}

export const salesItemPricingService = {
  get: (itemId: string) =>
    apiGet<SalesItemPricingDto>(`${BASE}/items/${itemId}/pricing`),
};
