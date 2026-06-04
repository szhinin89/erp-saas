import { apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';
import type {
  ItemDetailDto,
  ItemDto,
  ItemVariantDto,
  GetItemsResponse,
} from '../../../types/items';

export interface GetItemsParams {
  search?: string;
  sku?: string;
  isActive?: boolean;
  isForSale?: boolean;
  isFavorite?: boolean;
  isEcommerce?: boolean;
  itemType?: string;
  categoryNodeId?: string;
  brandId?: string;
  barcode?: string;
  pageNumber?: number;
  pageSize?: number;
}

export interface CreateItemRequest {
  sku: string;
  shortName: string;
  description: string;
  itemType: string;
  defaultUomCode: string;
  appliesVatOnSale: boolean;
  saleVatCode: string | null;
  vatAccountId: string | null;
  appliesVatOnPurchase: boolean;
  purchaseVatCode: string | null;
  purchaseVatAccountId: string | null;
  purchaseCode?: string | null;
  observations?: string | null;
  categoryNodeId?: string | null;
  brandId?: string | null;
  appliesExciseTax?: boolean;
  exciseTaxCode?: string | null;
  exciseAccountId?: string | null;
  sriServiceCode?: string | null;
  isForSale?: boolean;
  maxDiscountPercent?: number | null;
  isAvailableOnWeb?: boolean;
  isAvailableOnPOS?: boolean;
  isAvailableOnMobile?: boolean;
  isEcommerceActive?: boolean;
  tracksStock?: boolean;
  tracksLot?: boolean;
  tracksSeries?: boolean;
  allowDecimalQty?: boolean;
  allowDecimalSale?: boolean;
  minStockQty?: number | null;
  maxStockQty?: number | null;
}

export interface UpdateItemRequest extends Omit<CreateItemRequest, 'sku'> {
  id: string;
}

export interface AddVariantRequest {
  attributes: { attributeDefinitionId: string; value: string }[];
  skuOverride?: string | null;
  sortOrder?: number;
}

function buildParams(params: GetItemsParams): string {
  const q = new URLSearchParams();
  if (params.search) q.set('search', params.search);
  if (params.sku) q.set('sku', params.sku);
  if (params.isActive !== undefined) q.set('isActive', String(params.isActive));
  if (params.isForSale !== undefined) q.set('isForSale', String(params.isForSale));
  if (params.isFavorite !== undefined) q.set('isFavorite', String(params.isFavorite));
  if (params.isEcommerce !== undefined) q.set('isEcommerce', String(params.isEcommerce));
  if (params.itemType) q.set('itemType', params.itemType);
  if (params.categoryNodeId) q.set('categoryNodeId', params.categoryNodeId);
  if (params.brandId) q.set('brandId', params.brandId);
  if (params.barcode) q.set('barcode', params.barcode);
  q.set('pageNumber', String(params.pageNumber ?? 1));
  q.set('pageSize', String(params.pageSize ?? 20));
  return q.toString() ? `?${q.toString()}` : '';
}

export const itemService = {
  getAll: (params: GetItemsParams = {}) =>
    apiGet<GetItemsResponse>(`/api/items${buildParams(params)}`),

  getById: (id: string) =>
    apiGet<ItemDetailDto>(`/api/items/${id}`),

  create: (request: CreateItemRequest) =>
    apiPost<ItemDto>('/api/items', request),

  update: (request: UpdateItemRequest) => {
    const { id, ...body } = request;
    return apiPut<ItemDto>(`/api/items/${id}`, { id, ...body });
  },

  disable: (id: string) => apiPatch<boolean>(`/api/items/${id}/disable`),
  enable: (id: string)  => apiPatch<boolean>(`/api/items/${id}/enable`),

  addVariant: (itemId: string, request: AddVariantRequest) =>
    apiPost<ItemVariantDto>(`/api/items/${itemId}/variants`, request),

  disableVariant: (itemId: string, variantId: string) =>
    apiPatch<boolean>(`/api/items/${itemId}/variants/${variantId}/disable`),

  enableVariant: (itemId: string, variantId: string) =>
    apiPatch<boolean>(`/api/items/${itemId}/variants/${variantId}/enable`),
};
