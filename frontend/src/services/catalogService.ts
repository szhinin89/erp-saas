import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type CatalogItem = { id: string; code: string; name: string; isActive: boolean };
export type BrandItem = CatalogItem & { manufacturer?: string | null; countryOfOrigin?: string | null };
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

function getList<T>(url: string) {
  return api.get<ApiResponse<T>>(url).then((r) => r.data.responseObject);
}

function post<T>(url: string, body: unknown) {
  return api.post<ApiResponse<T>>(url, body).then((r) => r.data.responseObject);
}

function put<T>(url: string, body: unknown) {
  return api.put<ApiResponse<T>>(url, body).then((r) => r.data.responseObject);
}

function patch<T>(url: string) {
  return api.patch<ApiResponse<T>>(url, {}).then((r) => r.data.responseObject);
}

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
  brands: (onlyActive = true) => getList<BrandItem[]>(`/api/brands?onlyActive=${onlyActive}`),
  productTypes: (onlyActive = true) => getList<CatalogItem[]>(`/api/producttypes?onlyActive=${onlyActive}`),
  units: (onlyActive = true) => getList<CatalogItem[]>(`/api/unitsofmeasure?onlyActive=${onlyActive}`),
  taxRates: (onlyActive = true) => getList<CatalogItem[]>(`/api/taxrates?onlyActive=${onlyActive}`),
  tariffs: async (onlyActive = true) => {
    const data = await getList<TariffApiItem[]>(`/api/tariffs?onlyActive=${onlyActive}`);
    return (data ?? []).map((x) => ({
      id: x.id,
      code: x.code,
      name: x.description,
      isActive: x.isActive,
    }));
  },

  productLines: (opts?: { activeStatus?: CatalogActiveStatus; search?: string }) =>
    getList<CatalogItem[]>(
      `/api/productlines${catalogQuery({
        activeStatus: opts?.activeStatus ?? 'active',
        search: opts?.search,
      })}`
    ),

  categories: (opts?: { activeStatus?: CatalogActiveStatus; search?: string; lineId?: string }) =>
    getList<ProductCategoryListItem[]>(
      `/api/productcategories${catalogQuery({
        activeStatus: opts?.activeStatus ?? 'active',
        search: opts?.search,
        lineId: opts?.lineId,
      })}`
    ),

  subcategories: (opts?: {
    activeStatus?: CatalogActiveStatus;
    search?: string;
    lineId?: string;
    categoryId?: string;
  }) =>
    getList<ProductSubcategoryListItem[]>(
      `/api/productsubcategories${catalogQuery({
        activeStatus: opts?.activeStatus ?? 'active',
        search: opts?.search,
        lineId: opts?.lineId,
        categoryId: opts?.categoryId,
      })}`
    ),

  createBrand: (body: { code: string; name: string; manufacturer?: string | null; countryOfOrigin?: string | null }) =>
    post<BrandItem>('/api/brands', body),
  updateBrand: (id: string, body: { code: string; name: string; manufacturer?: string | null; countryOfOrigin?: string | null }) =>
    put<BrandItem>(`/api/brands/${encodeURIComponent(id)}`, { brandId: id, ...body }),
  disableBrand: (id: string) => patch<BrandItem>(`/api/brands/${encodeURIComponent(id)}/disable`),
  enableBrand:  (id: string) => patch<BrandItem>(`/api/brands/${encodeURIComponent(id)}/enable`),
  createProductType: (body: { code: string; name: string }) => post<CatalogItem>('/api/producttypes', body),
  updateProductType: (id: string, body: { code: string; name: string }) =>
    put<CatalogItem>(`/api/producttypes/${encodeURIComponent(id)}`, { productTypeId: id, ...body }),
  disableProductType: (id: string) => patch<CatalogItem>(`/api/producttypes/${encodeURIComponent(id)}/disable`),
  enableProductType:  (id: string) => patch<CatalogItem>(`/api/producttypes/${encodeURIComponent(id)}/enable`),
  createUnit: (body: { code: string; name: string; symbol?: string | null }) => post<CatalogItem>('/api/unitsofmeasure', body),
  createTaxRate: (body: { code: string; name: string; type: 'VAT' | 'Excise' | 'Other'; percentage: number }) =>
    post<CatalogItem>('/api/taxrates', body),
  createTariff: async (body: { code: string; name: string }) => {
    const created = await post<TariffApiItem>('/api/tariffs', {
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
  createProductLine: (body: { code: string; name: string }) => post<CatalogItem>('/api/productlines', body),
  createCategory: (body: { code: string; name: string; lineId: string }) => post<CatalogItem>('/api/productcategories', body),
  createSubcategory: (body: { code: string; name: string; categoryId: string }) => post<CatalogItem>('/api/productsubcategories', body),

  updateProductLine: (id: string, body: { code: string; name: string }) =>
    put<CatalogItem>(`/api/productlines/${encodeURIComponent(id)}`, { id, code: body.code, name: body.name }),
  disableProductLine: (id: string) => patch<CatalogItem>(`/api/productlines/${encodeURIComponent(id)}/disable`),
  enableProductLine: (id: string) => patch<CatalogItem>(`/api/productlines/${encodeURIComponent(id)}/enable`),

  updateCategory: (id: string, body: { code: string; name: string; lineId: string }) =>
    put<CatalogItem>(`/api/productcategories/${encodeURIComponent(id)}`, { id, code: body.code, name: body.name, lineId: body.lineId }),
  disableCategory: (id: string) => patch<CatalogItem>(`/api/productcategories/${encodeURIComponent(id)}/disable`),
  enableCategory: (id: string) => patch<CatalogItem>(`/api/productcategories/${encodeURIComponent(id)}/enable`),

  updateSubcategory: (id: string, body: { code: string; name: string; categoryId: string }) =>
    put<CatalogItem>(`/api/productsubcategories/${encodeURIComponent(id)}`, {
      id,
      code: body.code,
      name: body.name,
      categoryId: body.categoryId,
    }),
  disableSubcategory: (id: string) => patch<CatalogItem>(`/api/productsubcategories/${encodeURIComponent(id)}/disable`),
  enableSubcategory: (id: string) => patch<CatalogItem>(`/api/productsubcategories/${encodeURIComponent(id)}/enable`),
};
