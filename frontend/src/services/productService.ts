import { api } from '../lib/api';
import type { Product } from '../types/product';
import type { ApiResponse } from '../types/api';

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
}

export const productService = {
  getAll:   () => api.get<ApiResponse<Product[]>>('/api/products').then((r) => r.data.responseObject),
  getById:  (id: string) => api.get<ApiResponse<Product>>(`/api/products/${id}`).then((r) => r.data.responseObject),
  create:   (data: CreateProductRequest) => api.post<ApiResponse<Product>>('/api/products', data).then((r) => r.data.responseObject),
};
