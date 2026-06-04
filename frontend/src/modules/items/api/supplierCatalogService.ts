import { apiGet, apiPost } from '../../lib/apiEnvelope';
import type { SupplierItemDto } from '../../../types/items';

export interface RegisterSupplierItemRequest {
  supplierId: string;
  itemId: string;
  supplierCode: string;
  defaultUomCode: string;
  variantId?: string | null;
  supplierName?: string | null;
  leadTimeDays?: number;
  minOrderQty?: number;
  isDefault?: boolean;
}

export const supplierCatalogService = {
  getByItem: (itemId: string) =>
    apiGet<SupplierItemDto[]>(`/api/supplier-catalog/by-item/${itemId}`).then((d) => d ?? []),

  register: (request: RegisterSupplierItemRequest) =>
    apiPost<SupplierItemDto>('/api/supplier-catalog', request),
};
