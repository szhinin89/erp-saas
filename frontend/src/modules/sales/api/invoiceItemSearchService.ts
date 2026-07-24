import { apiGet } from '../../lib/apiEnvelope';

const BASE = '/api/v1/sales';

export interface InvoiceItemSearchResultDto {
  id: string;
  sku: string;
  description: string;
  productFamilyName: string | null;
  uomAbbrev: string;
  tracksStock: boolean;
  warehouseName: string | null;
  availableStock: number | null;
  averageCost: number | null;
  salePriceWithoutTax: number | null;
  finalSalePrice: number | null;
  vatDisplay: string;
  iceDisplay: string;
  vatCode: string | null;
  iceCode: string | null;
}

export const invoiceItemSearchService = {
  search: (params: { q: string; warehouseId?: string; pageSize?: number }) =>
    apiGet<InvoiceItemSearchResultDto[]>(`${BASE}/item-search`, { params }),
};
