import { apiGet, apiPut } from '../../../lib/apiEnvelope';

const BASE = '/api/v1/org-config';

// ── Empresa (propietario: DocTypeCode, PaymentMethodCode, PaymentTermId) ──
export interface CompanyInvoiceOrgSettingsDto {
  defaultDocTypeCode:          string | null;
  defaultSriPaymentMethodCode: string | null;
  defaultPaymentTermId:        string | null;
}

export interface UpsertCompanyInvoiceOrgSettingsPayload {
  defaultDocTypeCode:          string | null;
  defaultSriPaymentMethodCode: string | null;
  defaultPaymentTermId:        string | null;
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

  upsertCompanyInvoiceDefaults: (body: UpsertCompanyInvoiceOrgSettingsPayload) =>
    apiPut<CompanyInvoiceOrgSettingsDto>(`${BASE}/company/invoice-defaults`, body),

  getBranchInvoiceDefaults: (branchId: string) =>
    apiGet<BranchInvoiceOrgSettingsDto>(`${BASE}/branch/${branchId}/invoice-defaults`),

  upsertBranchInvoiceDefaults: (branchId: string, body: UpsertBranchInvoiceOrgSettingsPayload) =>
    apiPut<BranchInvoiceOrgSettingsDto>(`${BASE}/branch/${branchId}/invoice-defaults`, body),
};
