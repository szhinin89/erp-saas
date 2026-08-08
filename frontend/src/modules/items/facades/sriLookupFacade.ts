/**
 * sriLookupFacade — superficie pública read-only de catálogos SRI para
 * consumidores externos (configuración de empresa, formularios de compras/ventas).
 *
 * Expone únicamente los lookups GET de sriLookupService; nunca las otras
 * responsabilidades de catalogService.ts (brands/attributes, que sí mutan).
 * Los módulos externos deben importar desde aquí, nunca directamente de
 * items/catalog/api/catalogService.
 */

import { sriLookupService } from "../catalog/api/catalogService";
import type {
  SriDocTypeLookup,
  SriUomLookup,
  SriVatRateLookup,
  SriIceRateLookup,
  SriRetentionCodeLookup,
  SriTaxSupportLookup,
  SriPaymentMethodLookup,
  SriIdTypeLookup,
  SriSupplierTypeLookup,
  SriTaxRegimeLookup,
} from "../catalog/api/catalogService";

export type {
  SriDocTypeLookup,
  SriUomLookup,
  SriVatRateLookup,
  SriIceRateLookup,
  SriRetentionCodeLookup,
  SriTaxSupportLookup,
  SriPaymentMethodLookup,
  SriIdTypeLookup,
  SriSupplierTypeLookup,
  SriTaxRegimeLookup,
};

export const sriLookupFacade = {
  docTypes: sriLookupService.docTypes,
  uoms: sriLookupService.uoms,
  vatRates: sriLookupService.vatRates,
  iceRates: sriLookupService.iceRates,
  retentionCodes: sriLookupService.retentionCodes,
  taxSupportCodes: sriLookupService.taxSupportCodes,
  paymentMethods: sriLookupService.paymentMethods,
  idTypes: sriLookupService.idTypes,
  supplierTypes: sriLookupService.supplierTypes,
  taxRegimes: sriLookupService.taxRegimes,
};
