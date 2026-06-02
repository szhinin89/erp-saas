import { apiDelete, apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';
import type {
  BusinessPartnerApiRow,
  BusinessPartnerDto,
  BusinessPartnerPagedResult,
  CompanyBpSettingsDto,
  CreateBusinessPartnerBody,
  SearchBusinessPartnersParams,
  UpdateBusinessPartnerBody,
  UpdateSupplierProfileBody,
} from '../types/businessPartner.types';

/** Ruta canónica backend (`BusinessPartnersController`). */
export const BUSINESS_PARTNERS_API = '/api/master/business-partners';

function pickStr(row: BusinessPartnerApiRow, ...keys: (keyof BusinessPartnerApiRow)[]): string {
  for (const k of keys) {
    const v = row[k];
    if (typeof v === 'string' && v.length > 0) return v;
  }
  return '';
}

function pickGuid(row: BusinessPartnerApiRow, ...keys: (keyof BusinessPartnerApiRow)[]): string | null {
  for (const k of keys) {
    const v = row[k];
    if (typeof v === 'string' && v.length > 0) return v;
  }
  return null;
}

function normalizeRow(row: BusinessPartnerApiRow): BusinessPartnerDto {
  return {
    id:                   pickStr(row, 'id', 'Id'),
    identificationType:   pickStr(row, 'identificationType', 'IdentificationType'),
    identificationNumber: pickStr(row, 'identificationNumber', 'IdentificationNumber'),
    legalName:            pickStr(row, 'legalName', 'LegalName'),
    tradeName:                row.tradeName               ?? row.TradeName               ?? null,
    legalRepresentativeName:  row.legalRepresentativeName ?? row.LegalRepresentativeName ?? null,
    email:                row.email       ?? row.Email       ?? null,
    phone:                row.phone       ?? row.Phone       ?? null,
    countryCode:          row.countryCode ?? row.CountryCode ?? null,
    isActive:             row.isActive    ?? row.IsActive    ?? true,
    isCustomer:           row.isCustomer  ?? row.IsCustomer  ?? false,
    isSupplier:           row.isSupplier  ?? row.IsSupplier  ?? false,
    customerProfileId:    pickGuid(row, 'customerProfileId', 'CustomerProfileId'),
    supplierProfileId:    pickGuid(row, 'supplierProfileId', 'SupplierProfileId'),
    legacyCustomerId:     pickGuid(row, 'legacyCustomerId',  'LegacyCustomerId'),
    legacySupplierId:     pickGuid(row, 'legacySupplierId',  'LegacySupplierId'),
    customerNotes:              row.customerNotes              ?? row.CustomerNotes              ?? null,
    defaultTaxSupportCode:      row.defaultTaxSupportCode      ?? row.DefaultTaxSupportCode      ?? null,
    defaultRetentionVatCode:    row.defaultRetentionVatCode    ?? row.DefaultRetentionVatCode    ?? null,
    defaultRetentionIncomeCode: row.defaultRetentionIncomeCode ?? row.DefaultRetentionIncomeCode ?? null,
    supplierPaymentTerms:       row.supplierPaymentTerms       ?? row.SupplierPaymentTerms       ?? null,
  };
}

function buildSearchQuery(params: SearchBusinessPartnersParams): string {
  const q = new URLSearchParams();
  if (params.q?.trim()) q.set('q', params.q.trim());
  if (params.isActive !== undefined) q.set('isActive', String(params.isActive));
  if (params.isCustomer !== undefined) q.set('isCustomer', String(params.isCustomer));
  if (params.isSupplier !== undefined) q.set('isSupplier', String(params.isSupplier));
  if (params.skip !== undefined) q.set('skip', String(params.skip));
  if (params.take !== undefined) q.set('take', String(params.take));
  const qs = q.toString();
  return qs ? `?${qs}` : '';
}

type PagedApiResponse = { items: BusinessPartnerApiRow[]; pageNumber: number; pageSize: number; totalCount: number };

export const businessPartnerService = {
  searchPaged: async (params: SearchBusinessPartnersParams = {}): Promise<BusinessPartnerPagedResult> => {
    const raw = await apiGet<PagedApiResponse>(
      `${BUSINESS_PARTNERS_API}${buildSearchQuery(params)}`,
    );
    const items = (raw?.items ?? []).map(normalizeRow);
    return {
      items,
      pageNumber:  raw?.pageNumber  ?? 1,
      pageSize:    raw?.pageSize    ?? params.take ?? 50,
      totalCount:  raw?.totalCount  ?? items.length,
    };
  },

  search: async (params: SearchBusinessPartnersParams = {}): Promise<BusinessPartnerDto[]> => {
    const paged = await businessPartnerService.searchPaged(params);
    return paged.items;
  },

  getById: async (id: string): Promise<BusinessPartnerDto> => {
    const row = await apiGet<BusinessPartnerApiRow>(`${BUSINESS_PARTNERS_API}/${encodeURIComponent(id)}`);
    return normalizeRow(row);
  },

  create: (body: CreateBusinessPartnerBody) =>
    apiPost<BusinessPartnerApiRow>(BUSINESS_PARTNERS_API, body).then(normalizeRow),

  disable: (id: string) => apiDelete<boolean>(`${BUSINESS_PARTNERS_API}/${encodeURIComponent(id)}`),

  update: (id: string, body: UpdateBusinessPartnerBody) =>
    apiPut<boolean>(`${BUSINESS_PARTNERS_API}/${encodeURIComponent(id)}`, body),

  activate: (id: string) =>
    apiPatch<boolean>(`${BUSINESS_PARTNERS_API}/${encodeURIComponent(id)}/activate`, {}),

  getCompanySettings: async (id: string): Promise<CompanyBpSettingsDto | null> => {
    const raw = await apiGet<CompanyBpSettingsDto | null>(
      `${BUSINESS_PARTNERS_API}/${encodeURIComponent(id)}/company-settings`,
    );
    return raw ?? null;
  },

  upsertCompanySettings: (
    id: string,
    body: { creditLimit?: number | null; paymentDays: number; isBlocked: boolean },
  ) =>
    apiPatch<boolean>(`${BUSINESS_PARTNERS_API}/${encodeURIComponent(id)}/company-settings`, body),

  addRole: (id: string, asCustomer: boolean, asSupplier: boolean) =>
    apiPatch<boolean>(`${BUSINESS_PARTNERS_API}/${encodeURIComponent(id)}/add-role`, { asCustomer, asSupplier }),

  updateCustomerNotes: (id: string, notes: string | null) =>
    apiPatch<boolean>(`${BUSINESS_PARTNERS_API}/${encodeURIComponent(id)}/customer-notes`, { notes }),

  updateSupplierProfile: (id: string, body: UpdateSupplierProfileBody) =>
    apiPatch<boolean>(`${BUSINESS_PARTNERS_API}/${encodeURIComponent(id)}/supplier-profile`, body),
};
