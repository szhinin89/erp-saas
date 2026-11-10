import { apiGet, apiPut } from "../../../lib/apiEnvelope";

const BASE = "/api/v1/org-config";

// ── Empresa (propietario: DocTypeCode, PaymentMethodCode, PaymentTermId) ──
// defaultPriceListId es una proyección de PriceList.IsDefault (no un OrgSetting).
export interface CompanyInvoiceOrgSettingsDto {
  defaultDocTypeCode: string | null;
  defaultSriPaymentMethodCode: string | null;
  defaultPaymentTermId: string | null;
  defaultPriceListId: string | null;
  configurationWarnings: string[];
  /** Tipos de documento SRI aceptados como default de ventas (política del backend). */
  allowedDocTypeCodes: string[];
}

/**
 * Guardado atómico de Configuración → Ventas. `defaultPriceList` ausente = no tocar la lista
 * predeterminada; presente = reasignarla (`priceListId: null` la quita). Exige además permiso
 * de Pricing (pricing.update).
 */
export interface UpsertCompanyInvoiceOrgSettingsPayload {
  defaultDocTypeCode: string | null;
  defaultSriPaymentMethodCode: string | null;
  defaultPaymentTermId: string | null;
  defaultPriceList?: { priceListId: string | null };
}

// ── Sucursal (propietario: DefaultWarehouseId) ────────────────────────────
export interface BranchInvoiceOrgSettingsDto {
  defaultWarehouseId: string | null;
}

export interface UpsertBranchInvoiceOrgSettingsPayload {
  defaultWarehouseId: string | null;
}

export const orgConfigService = {
  getCompanyInvoiceDefaults: () =>
    apiGet<CompanyInvoiceOrgSettingsDto>(`${BASE}/company/invoice-defaults`),

  upsertCompanyInvoiceDefaults: (
    body: UpsertCompanyInvoiceOrgSettingsPayload,
  ) =>
    apiPut<CompanyInvoiceOrgSettingsDto>(
      `${BASE}/company/invoice-defaults`,
      body,
    ),

  getBranchInvoiceDefaults: (branchId: string) =>
    apiGet<BranchInvoiceOrgSettingsDto>(
      `${BASE}/branch/${branchId}/invoice-defaults`,
    ),

  upsertBranchInvoiceDefaults: (
    branchId: string,
    body: UpsertBranchInvoiceOrgSettingsPayload,
  ) =>
    apiPut<BranchInvoiceOrgSettingsDto>(
      `${BASE}/branch/${branchId}/invoice-defaults`,
      body,
    ),
};
