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
}

export const salesItemPricingService = {
  get: (itemId: string) =>
    apiGet<SalesItemPricingDto>(`${BASE}/items/${itemId}/pricing`),
};
