import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';
import type { Product } from '../../../types/product';

export interface CreateProductRequest {
  // Identificación básica
  saleCode: string;
  purchaseCode?: string;
  barcodes?: Array<{ code: string; type: number }>;
  shortName: string;
  description: string;
  observations?: string;

  // Categorización (3 niveles)
  lineId: string;
  categoryId: string;
  subcategoryId: string;

  // Catálogos relacionados
  unitOfMeasureId: string;
  brandId: string;
  productTypeId: string;
  tariffId: string;

  // Impuestos
  appliesVatOnSale: boolean;
  appliesVatOnPurchase: boolean;
  appliesExciseTax: boolean;
  saleTaxId: string | null;
  purchaseTaxId: string | null;
  exciseTaxId: string | null;
  saleVatAccountId: string | null;
  purchaseVatAccountId: string | null;
  exciseAccountId: string | null;

  // Comportamiento de stock
  isService: boolean;
  tracksStock: boolean;
  tracksLot: boolean;
  tracksSeries: boolean;
  hasRecipe: boolean;
  stockWithDecimal: boolean;
  saleWithDecimal: boolean;
  maxItemDiscountPercent: number;

  // Canales de venta
  availableOnWeb: boolean;
  availableOnMobile: boolean;
  isEcommerceActive: boolean;
  isFavorite: boolean;
  isForSale: boolean;

  // Variantes
  baseColor?: string;
  hasMultipleColors: boolean;
  hasSizes: boolean;

  // Aranceles
  handlesTariff: boolean;
}

export interface UpdateProductRequest extends CreateProductRequest {
  id: string;
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

  async update(request: UpdateProductRequest): Promise<Product> {
    const { id, ...updateData } = request;
    const response = await api.put<ApiResponse<Product>>(`/api/products/${id}`, { id, ...updateData });
    return response.data.responseObject;
  },

  async disable(id: string): Promise<Product> {
    const response = await api.patch<ApiResponse<Product>>(`/api/products/${id}/disable`);
    return response.data.responseObject;
  },

  async enable(id: string): Promise<Product> {
    const response = await api.patch<ApiResponse<Product>>(`/api/products/${id}/enable`);
    return response.data.responseObject;
  },
};
