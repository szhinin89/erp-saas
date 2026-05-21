import { api } from '../modules/lib/api';
import type { Product } from '../types/product';
import type { ApiResponse } from '../types/api';

/** Coincide con `ImageInput` del API (crear producto). */
export interface ProductImageInput {
  url: string;
  altText?: string | null;
  isMain: boolean;
  isEcommerce: boolean;
  sortOrder: number;
}

export interface CreateProductRequest {
  saleCode: string;
  purchaseCode?: string;
  shortName: string;
  description: string;
  lineId: string;
  categoryId: string;
  subcategoryId: string;
  unitOfMeasureId: string;
  brandId: string;
  productTypeId: string;
  tariffId: string;
  saleTaxId: string;
  purchaseTaxId: string;
  exciseTaxId?: string;
  isService: boolean;
  isForSale: boolean;
  availableOnWeb: boolean;
  availableOnMobile: boolean;
  images?: ProductImageInput[];
}

export const productService = {
  getAll:   () => api.get<ApiResponse<Product[]>>('/api/inventory/products').then((r) => r.data.responseObject),
  getById:  (id: string) => api.get<ApiResponse<Product>>(`/api/inventory/products/${id}`).then((r) => r.data.responseObject),
  create:   (data: CreateProductRequest) => api.post<ApiResponse<Product>>('/api/inventory/products', data).then((r) => r.data.responseObject),
};
