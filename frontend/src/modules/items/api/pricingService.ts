import { apiGet, apiPost } from '../../lib/apiEnvelope';
import type { PriceListDto, PriceListEntryDto, ResolvedPriceDto } from '../../../types/items';

export interface CreatePriceListRequest {
  code: string;
  name: string;
  currencyCode: string;
  validFrom: string;
  priceIncludesVat?: boolean;
  validTo?: string | null;
  isDefault?: boolean;
  target?: string;
}

export interface SetItemPriceRequest {
  itemId: string;
  uomCode: string;
  price: number;
  minQty?: number;
  variantId?: string | null;
}

export const pricingService = {
  getPriceLists: () =>
    apiGet<PriceListDto[]>('/api/pricing/price-lists').then((d) => d ?? []),

  createPriceList: (request: CreatePriceListRequest) =>
    apiPost<PriceListDto>('/api/pricing/price-lists', request),

  setItemPrice: (priceListId: string, request: SetItemPriceRequest) =>
    apiPost<PriceListEntryDto>(`/api/pricing/price-lists/${priceListId}/entries`, request),

  resolvePrice: (itemId: string, uomCode: string, qty = 1, variantId?: string) => {
    const q = new URLSearchParams({ itemId, uomCode, qty: String(qty) });
    if (variantId) q.set('variantId', variantId);
    return apiGet<ResolvedPriceDto>(`/api/pricing/resolve?${q.toString()}`);
  },
};
