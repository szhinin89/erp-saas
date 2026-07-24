import { apiGet, apiPatch, apiPost, apiPut } from '../../../lib/apiEnvelope';

const BASE = '/api/v1/catalog';

// ── DTOs ─────────────────────────────────────────────────────────────────

export interface BrandDto {
  id: string;
  code: string;
  name: string;
  manufacturer: string | null;
  countryOfOrigin: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface AttributeGroupDto {
  id: string;
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}

export interface AttributeDefinitionDto {
  id: string;
  groupId: string;
  code: string;
  name: string;
  dataType: string;
  isVariantAxis: boolean;
  allowedValues: string | null;
  isRequired: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface SriUomLookup { code: string; name: string; abbrev: string | null; }
export interface SriVatRateLookup { code: string; name: string; percentage: number; }
export interface SriIceRateLookup { code: string; name: string; percentage: number; }
export interface SriRetentionCodeLookup { id: string; taxType: string; code: string; name: string; percentage: number; appliesTo: string; }
export interface SriTaxSupportLookup { code: string; name: string; }
export interface SriPaymentMethodLookup { code: string; name: string; }
export interface SriDocTypeLookup { code: string; name: string; shortName: string; isElectronic: boolean; }
export interface SriIdTypeLookup { code: string; name: string; digits: number | null; }
export interface SriSupplierTypeLookup { code: string; name: string; }
export interface SriTaxRegimeLookup { code: string; name: string; abbrev: string | null; }

// ── Payloads ─────────────────────────────────────────────────────────────

export interface CreateBrandPayload { code: string; name: string; manufacturer?: string | null; countryOfOrigin?: string | null; }
export interface UpdateBrandPayload extends CreateBrandPayload { id: string; }

export interface CreateAttributeGroupPayload { code: string; name: string; sortOrder?: number; }
export interface UpdateAttributeGroupPayload extends CreateAttributeGroupPayload { id: string; }

export interface CreateAttributeDefinitionPayload {
  groupId: string; code: string; name: string; dataType: string;
  isVariantAxis?: boolean; allowedValues?: string | null; isRequired?: boolean; sortOrder?: number;
}
export interface UpdateAttributeDefinitionPayload extends CreateAttributeDefinitionPayload { id: string; }

// ── SRI Lookups ──────────────────────────────────────────────────────────

export const sriLookupService = {
  docTypes:        () => apiGet<SriDocTypeLookup[]>(`${BASE}/sri-doc-types`),
  uoms:            () => apiGet<SriUomLookup[]>(`${BASE}/sri-uom`),
  vatRates:        () => apiGet<SriVatRateLookup[]>(`${BASE}/sri-vat-rates`),
  iceRates:        () => apiGet<SriIceRateLookup[]>(`${BASE}/sri-ice-rates`),
  retentionCodes:  (taxType?: string) => apiGet<SriRetentionCodeLookup[]>(`${BASE}/sri-retention-codes${taxType ? `?taxType=${taxType}` : ''}`),
  taxSupportCodes: () => apiGet<SriTaxSupportLookup[]>(`${BASE}/sri-tax-support-codes`),
  paymentMethods:  () => apiGet<SriPaymentMethodLookup[]>(`${BASE}/sri-payment-methods`),
  idTypes:         (usage?: string) => apiGet<SriIdTypeLookup[]>(`${BASE}/sri-id-types${usage ? `/by-usage/${usage}` : ''}`),
  supplierTypes: () => apiGet<SriSupplierTypeLookup[]>(`${BASE}/sri-supplier-types`),
  taxRegimes:    () => apiGet<SriTaxRegimeLookup[]>(`${BASE}/sri-tax-regimes`),
};

// ── Brands ───────────────────────────────────────────────────────────────

export const brandService = {
  list:    (isActive?: boolean) => apiGet<BrandDto[]>(`${BASE}/brands${isActive !== undefined ? `?isActive=${isActive}` : ''}`),
  getById: (id: string) => apiGet<BrandDto>(`${BASE}/brands/${id}`),
  create:  (p: CreateBrandPayload) => apiPost<BrandDto>(`${BASE}/brands`, p),
  update:  (id: string, p: UpdateBrandPayload) => apiPut<BrandDto>(`${BASE}/brands/${id}`, p),
  enable:  (id: string) => apiPatch<boolean>(`${BASE}/brands/${id}/enable`),
  disable: (id: string) => apiPatch<boolean>(`${BASE}/brands/${id}/disable`),
};

// ── Attribute Groups ─────────────────────────────────────────────────────

export const attributeGroupService = {
  list:    (isActive?: boolean) => apiGet<AttributeGroupDto[]>(`${BASE}/attribute-groups${isActive !== undefined ? `?isActive=${isActive}` : ''}`),
  getById: (id: string) => apiGet<AttributeGroupDto>(`${BASE}/attribute-groups/${id}`),
  create:  (p: CreateAttributeGroupPayload) => apiPost<AttributeGroupDto>(`${BASE}/attribute-groups`, p),
  update:  (id: string, p: UpdateAttributeGroupPayload) => apiPut<AttributeGroupDto>(`${BASE}/attribute-groups/${id}`, p),
  enable:  (id: string) => apiPatch<boolean>(`${BASE}/attribute-groups/${id}/enable`),
  disable: (id: string) => apiPatch<boolean>(`${BASE}/attribute-groups/${id}/disable`),
};

// ── Attribute Definitions ────────────────────────────────────────────────

export const attributeDefinitionService = {
  list:    (groupId?: string, isActive?: boolean) => {
    const params = new URLSearchParams();
    if (groupId) params.set('groupId', groupId);
    if (isActive !== undefined) params.set('isActive', String(isActive));
    const qs = params.toString();
    return apiGet<AttributeDefinitionDto[]>(`${BASE}/attribute-definitions${qs ? `?${qs}` : ''}`);
  },
  getById: (id: string) => apiGet<AttributeDefinitionDto>(`${BASE}/attribute-definitions/${id}`),
  create:  (p: CreateAttributeDefinitionPayload) => apiPost<AttributeDefinitionDto>(`${BASE}/attribute-definitions`, p),
  update:  (id: string, p: UpdateAttributeDefinitionPayload) => apiPut<AttributeDefinitionDto>(`${BASE}/attribute-definitions/${id}`, p),
  enable:  (id: string) => apiPatch<boolean>(`${BASE}/attribute-definitions/${id}/enable`),
  disable: (id: string) => apiPatch<boolean>(`${BASE}/attribute-definitions/${id}/disable`),
};
