import { apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';
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
  getAll: () => apiGet<Product[]>('/api/inventory/products').then((rows) => rows ?? []),

  create: (request: CreateProductRequest) => apiPost<Product>('/api/inventory/products', request),

  update: (request: UpdateProductRequest) => {
    const { id, ...updateData } = request;
    return apiPut<Product>(`/api/inventory/products/${id}`, { id, ...updateData });
  },

  disable: (id: string) => apiPatch<Product>(`/api/inventory/products/${id}/disable`),

  enable: (id: string) => apiPatch<Product>(`/api/inventory/products/${id}/enable`),
};
