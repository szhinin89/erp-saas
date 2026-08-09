/**
 * classificationCatalogService — acceso HTTP a los 12 catálogos persistidos de clasificación de
 * BusinessPartner (CLASS-BP-CATALOGS-01): api/v1/catalog/classifications/*. Reemplazan los 12
 * arrays hardcodeados que vivían en businessPartner.types.ts. Solo lectura (GET) — CRUD
 * administrativo queda fuera de alcance de este bloque.
 */

import { apiGet } from "../../lib/apiEnvelope";

const BASE = "/api/v1/catalog/classifications";

export interface ClassificationCatalogItem {
  id: string;
  code: string;
  name: string;
  sortOrder: number;
}

export const classificationCatalogService = {
  customerCategories: () =>
    apiGet<ClassificationCatalogItem[]>(`${BASE}/customer-categories`),
  customerSegments: () =>
    apiGet<ClassificationCatalogItem[]>(`${BASE}/customer-segments`),
  customerCreditRatings: () =>
    apiGet<ClassificationCatalogItem[]>(`${BASE}/customer-credit-ratings`),
  loyaltyTiers: () => apiGet<ClassificationCatalogItem[]>(`${BASE}/loyalty-tiers`),
  customerInvoiceFormats: () =>
    apiGet<ClassificationCatalogItem[]>(`${BASE}/customer-invoice-formats`),
  customerClassifications: () =>
    apiGet<ClassificationCatalogItem[]>(`${BASE}/customer-classifications`),
  supplierCategories: () =>
    apiGet<ClassificationCatalogItem[]>(`${BASE}/supplier-categories`),
  supplierTypes: () => apiGet<ClassificationCatalogItem[]>(`${BASE}/supplier-types`),
  supplierRisks: () => apiGet<ClassificationCatalogItem[]>(`${BASE}/supplier-risks`),
  supplierRatings: () => apiGet<ClassificationCatalogItem[]>(`${BASE}/supplier-ratings`),
  primaryGoodTypes: () =>
    apiGet<ClassificationCatalogItem[]>(`${BASE}/primary-good-types`),
  supplierSegments: () =>
    apiGet<ClassificationCatalogItem[]>(`${BASE}/supplier-segments`),
};
