import { apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';

export type CatalogItem = { id: string; code: string; name: string; isActive: boolean };
export type BrandItem = CatalogItem & { manufacturer?: string | null; countryOfOrigin?: string | null };
export type UnitItem = CatalogItem & { symbol?: string | null };
type TariffApiItem = { id: string; code: string; description: string; isActive: boolean };

export type CatalogActiveStatus = 'all' | 'active' | 'inactive';

export type ProductCategoryListItem = CatalogItem & {
  lineId: string;
  lineCode: string;
  lineName: string;
};

export type ProductSubcategoryListItem = CatalogItem & {
  categoryId: string;
  lineId: string;
  lineCode: string;
  lineName: string;
  categoryCode: string;
  categoryName: string;
};

function catalogQuery(params: {
  activeStatus?: CatalogActiveStatus;
  search?: string;
  lineId?: string;
  categoryId?: string;
}) {
  const q = new URLSearchParams();
  if (params.activeStatus) q.set('activeStatus', params.activeStatus);
  if (params.search?.trim()) q.set('search', params.search.trim());
  if (params.lineId) q.set('lineId', params.lineId);
  if (params.categoryId) q.set('categoryId', params.categoryId);
  const s = q.toString();
  return s ? `?${s}` : '';
}

export const catalogService = {
  brands: (onlyActive = true) => apiGet<BrandItem[]>(`/api/inventory/brands?onlyActive=${onlyActive}`),

  productTypes: (onlyActive = true) => apiGet<CatalogItem[]>(`/api/inventory/product-types?onlyActive=${onlyActive}`),

  units: (onlyActive = true) => apiGet<UnitItem[]>(`/api/inventory/units?onlyActive=${onlyActive}`),

  taxRates: (onlyActive = true) => apiGet<CatalogItem[]>(`/api/taxrates?onlyActive=${onlyActive}`),

  tariffs: async (onlyActive = true) => {
    const data = await apiGet<TariffApiItem[]>(`/api/inventory/tariffs?onlyActive=${onlyActive}`);
    return (data ?? []).map((x) => ({
      id: x.id,
      code: x.code,
      name: x.description,
      isActive: x.isActive,
    }));
  },

  productLines: (opts?: { activeStatus?: CatalogActiveStatus; search?: string }) =>
    apiGet<CatalogItem[]>(
      `/api/inventory/product-lines${catalogQuery({
        activeStatus: opts?.activeStatus ?? 'active',
        search: opts?.search,
      })}`,
    ),

  categories: (opts?: { activeStatus?: CatalogActiveStatus; search?: string; lineId?: string }) =>
    apiGet<ProductCategoryListItem[]>(
      `/api/inventory/categories${catalogQuery({
        activeStatus: opts?.activeStatus ?? 'active',
        search: opts?.search,
        lineId: opts?.lineId,
      })}`,
    ),

  subcategories: (opts?: {
    activeStatus?: CatalogActiveStatus;
    search?: string;
    lineId?: string;
    categoryId?: string;
  }) =>
    apiGet<ProductSubcategoryListItem[]>(
      `/api/inventory/subcategories${catalogQuery({
        activeStatus: opts?.activeStatus ?? 'active',
        search: opts?.search,
        lineId: opts?.lineId,
        categoryId: opts?.categoryId,
      })}`,
    ),

  createBrand: (body: { code: string; name: string; manufacturer?: string | null; countryOfOrigin?: string | null }) =>
    apiPost<BrandItem>('/api/inventory/brands', body),

  updateBrand: (id: string, body: { code: string; name: string; manufacturer?: string | null; countryOfOrigin?: string | null }) =>
    apiPut<BrandItem>(`/api/inventory/brands/${encodeURIComponent(id)}`, { brandId: id, ...body }),

  disableBrand: (id: string) => apiPatch<BrandItem>(`/api/inventory/brands/${encodeURIComponent(id)}/disable`),

  enableBrand: (id: string) => apiPatch<BrandItem>(`/api/inventory/brands/${encodeURIComponent(id)}/enable`),

  createProductType: (body: { code: string; name: string }) => apiPost<CatalogItem>('/api/inventory/product-types', body),

  updateProductType: (id: string, body: { code: string; name: string }) =>
    apiPut<CatalogItem>(`/api/inventory/product-types/${encodeURIComponent(id)}`, { productTypeId: id, ...body }),

  disableProductType: (id: string) => apiPatch<CatalogItem>(`/api/inventory/product-types/${encodeURIComponent(id)}/disable`),

  enableProductType: (id: string) => apiPatch<CatalogItem>(`/api/inventory/product-types/${encodeURIComponent(id)}/enable`),

  createUnit: (body: { code: string; name: string; symbol?: string | null }) => apiPost<UnitItem>('/api/inventory/units', body),

  updateUnit: (id: string, body: { code: string; name: string; symbol?: string | null }) =>
    apiPut<UnitItem>(`/api/inventory/units/${encodeURIComponent(id)}`, { unitOfMeasureId: id, ...body }),

  disableUnit: (id: string) => apiPatch<UnitItem>(`/api/inventory/units/${encodeURIComponent(id)}/disable`),

  enableUnit: (id: string) => apiPatch<UnitItem>(`/api/inventory/units/${encodeURIComponent(id)}/enable`),

  createTaxRate: (body: { code: string; name: string; type: 'VAT' | 'Excise' | 'Other'; percentage: number }) =>
    apiPost<CatalogItem>('/api/taxrates', body),

  createTariff: async (body: { code: string; name: string }) => {
    const created = await apiPost<TariffApiItem>('/api/inventory/tariffs', {
      code: body.code,
      description: body.name,
    });
    return {
      id: created.id,
      code: created.code,
      name: created.description,
      isActive: created.isActive,
    } satisfies CatalogItem;
  },

  createProductLine: (body: { code: string; name: string }) => apiPost<CatalogItem>('/api/inventory/product-lines', body),

  createCategory: (body: { code: string; name: string; lineId: string }) => apiPost<CatalogItem>('/api/inventory/categories', body),

  createSubcategory: (body: { code: string; name: string; categoryId: string }) =>
    apiPost<CatalogItem>('/api/inventory/subcategories', body),

  updateProductLine: (id: string, body: { code: string; name: string }) =>
    apiPut<CatalogItem>(`/api/inventory/product-lines/${encodeURIComponent(id)}`, { id, code: body.code, name: body.name }),

  disableProductLine: (id: string) => apiPatch<CatalogItem>(`/api/inventory/product-lines/${encodeURIComponent(id)}/disable`),

  enableProductLine: (id: string) => apiPatch<CatalogItem>(`/api/inventory/product-lines/${encodeURIComponent(id)}/enable`),

  updateCategory: (id: string, body: { code: string; name: string; lineId: string }) =>
    apiPut<CatalogItem>(`/api/inventory/categories/${encodeURIComponent(id)}`, {
      id,
      code: body.code,
      name: body.name,
      lineId: body.lineId,
    }),

  disableCategory: (id: string) => apiPatch<CatalogItem>(`/api/inventory/categories/${encodeURIComponent(id)}/disable`),

  enableCategory: (id: string) => apiPatch<CatalogItem>(`/api/inventory/categories/${encodeURIComponent(id)}/enable`),

  updateSubcategory: (id: string, body: { code: string; name: string; categoryId: string }) =>
    apiPut<CatalogItem>(`/api/inventory/subcategories/${encodeURIComponent(id)}`, {
      id,
      code: body.code,
      name: body.name,
      categoryId: body.categoryId,
    }),

  disableSubcategory: (id: string) => apiPatch<CatalogItem>(`/api/inventory/subcategories/${encodeURIComponent(id)}/disable`),

  enableSubcategory: (id: string) => apiPatch<CatalogItem>(`/api/inventory/subcategories/${encodeURIComponent(id)}/enable`),
};
