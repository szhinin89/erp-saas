/**
 * classificationCatalogService — acceso HTTP a los 6 catálogos persistidos de clasificación de
 * Customer (CLASS-BP-CATALOGS-01): api/v1/catalog/classifications/*. Reemplazan los arrays
 * hardcodeados que vivían en businessPartner.types.ts. Solo lectura (GET) — CRUD administrativo
 * queda fuera de alcance de este bloque.
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
};
