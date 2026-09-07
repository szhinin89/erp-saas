/**
 * classificationCatalogFacade — superficie pública read-only de los 6 catálogos de clasificación
 * de Customer (CLASS-BP-CATALOGS-01) para consumidores externos (formulario de cliente en
 * MasterDataCustomersPage).
 *
 * Expone únicamente los lookups GET de classificationCatalogService; no hay mutación porque el
 * CRUD administrativo de estos catálogos queda fuera de alcance de este bloque (bloque futuro).
 * Mismo patrón que items/facades/sriLookupFacade.ts.
 */

import { classificationCatalogService } from "../api/classificationCatalogService";
import type { ClassificationCatalogItem } from "../api/classificationCatalogService";

export type { ClassificationCatalogItem };

export const classificationCatalogFacade = {
  customerCategories: classificationCatalogService.customerCategories,
  customerSegments: classificationCatalogService.customerSegments,
  customerCreditRatings: classificationCatalogService.customerCreditRatings,
  loyaltyTiers: classificationCatalogService.loyaltyTiers,
  customerInvoiceFormats: classificationCatalogService.customerInvoiceFormats,
  customerClassifications: classificationCatalogService.customerClassifications,
};
