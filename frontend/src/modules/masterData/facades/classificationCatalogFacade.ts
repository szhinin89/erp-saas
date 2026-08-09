/**
 * classificationCatalogFacade — superficie pública read-only de los 12 catálogos de clasificación
 * de BusinessPartner (CLASS-BP-CATALOGS-01) para consumidores externos (formularios de cliente y
 * proveedor en MasterDataCustomersPage / MasterDataSuppliersPage).
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
  supplierCategories: classificationCatalogService.supplierCategories,
  supplierTypes: classificationCatalogService.supplierTypes,
  supplierRisks: classificationCatalogService.supplierRisks,
  supplierRatings: classificationCatalogService.supplierRatings,
  primaryGoodTypes: classificationCatalogService.primaryGoodTypes,
  supplierSegments: classificationCatalogService.supplierSegments,
};
