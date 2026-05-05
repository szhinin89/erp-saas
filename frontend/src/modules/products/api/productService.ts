import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';
import type { Product } from '../../../types/product';

export interface CreateProductRequest {
  saleCode: string;
  shortName: string;
  description: string;
  lineId: string;
  categoryId: string;
  subcategoryId: string;
  unitOfMeasureId: string;
  brandId: string;
  productTypeId: string;
  tariffId: string;
  appliesVatOnSale: boolean;
  saleTaxId: string | null;
  saleVatAccountId: string | null;
  appliesVatOnPurchase: boolean;
  purchaseTaxId: string | null;
  purchaseVatAccountId: string | null;
}

export const productService = {
  async getAll(): Promise<Product[]> {
    const response = await api.get<ApiResponse<Product[]>>('/api/products');
    return response.data.responseObject ?? [];
  },

  async create(request: CreateProductRequest): Promise<Product> {
    const response = await api.post<ApiResponse<Product>>('/api/products', request);
    return response.data.responseObject;
  },
};
