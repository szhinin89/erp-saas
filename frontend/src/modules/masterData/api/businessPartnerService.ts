import {
  apiDelete,
  apiGet,
  apiPatch,
  apiPost,
  apiPut,
} from "../../lib/apiEnvelope";
import type {
  AssignRoleBody,
  BlockBody,
  BpContactDto,
  BpLocationDto,
  BusinessPartnerDetailDto,
  BusinessPartnerPagedResult,
  BusinessPartnerRoleDto,
  BusinessPartnerSummaryDto,
  CarrierConfigBody,
  CompanyBpTradingSettingsDto,
  CreateBusinessPartnerBody,
  CreateContactBody,
  CreateLocationBody,
  CustomerConfigBody,
  SearchBusinessPartnersParams,
  SupplierClassificationBody,
  SupplierConfigBody,
  UpdateBusinessPartnerBody,
  UpdateContactBody,
  UpdateIdentificationBody,
  UpdateLocationBody,
  UpdateRoleNotesBody,
  UpsertTradingSettingsBody,
} from "../types/businessPartner.types";

const BASE = "/api/v1/master/business-partners";
const enc = encodeURIComponent;

// ── Query string helpers ──────────────────────────────────────────────────────

export function buildSearchQuery(p: SearchBusinessPartnersParams): string {
  const q = new URLSearchParams();
  if (p.q?.trim()) q.set("q", p.q.trim());
  if (p.isActive !== undefined) q.set("isActive", String(p.isActive));
  if (p.skip !== undefined) q.set("skip", String(p.skip));
  if (p.take !== undefined) q.set("take", String(p.take));
  // roles: repeated param → ?roles=1&roles=2
  p.roles?.forEach((r) => q.append("roles", String(r)));
  const qs = q.toString();
  return qs ? `?${qs}` : "";
}

// ── BusinessPartner Identity ──────────────────────────────────────────────────

export const businessPartnerService = {
  /** GET /api/master/business-partners — búsqueda paginada */
  searchPaged: (
    params: SearchBusinessPartnersParams = {},
  ): Promise<BusinessPartnerPagedResult> =>
    apiGet<BusinessPartnerPagedResult>(`${BASE}${buildSearchQuery(params)}`),

  /** Búsqueda simple — devuelve la lista de items sin metadata de paginación */
  search: async (
    params: SearchBusinessPartnersParams = {},
  ): Promise<BusinessPartnerSummaryDto[]> => {
    const result = await businessPartnerService.searchPaged(params);
    return result.items;
  },

  /** GET /api/master/business-partners/{id} — detalle con roles */
  getById: (id: string): Promise<BusinessPartnerDetailDto> =>
    apiGet<BusinessPartnerDetailDto>(`${BASE}/${enc(id)}`),

  /** POST /api/master/business-partners */
  create: (
    body: CreateBusinessPartnerBody,
  ): Promise<BusinessPartnerSummaryDto> =>
    apiPost<BusinessPartnerSummaryDto>(BASE, body),

  /** PUT /api/master/business-partners/{id} */
  updateProfile: (
    id: string,
    body: UpdateBusinessPartnerBody,
  ): Promise<BusinessPartnerSummaryDto> =>
    apiPut<BusinessPartnerSummaryDto>(`${BASE}/${enc(id)}`, body),

  /** PATCH /api/master/business-partners/{id}/identification */
  updateIdentification: (
    id: string,
    body: UpdateIdentificationBody,
  ): Promise<BusinessPartnerSummaryDto> =>
    apiPatch<BusinessPartnerSummaryDto>(
      `${BASE}/${enc(id)}/identification`,
      body,
    ),

  /** PATCH /api/master/business-partners/{id}/activate */
  activate: (id: string): Promise<boolean> =>
    apiPatch<boolean>(`${BASE}/${enc(id)}/activate`),

  /** DELETE /api/master/business-partners/{id} */
  deactivate: (id: string): Promise<boolean> =>
    apiDelete<boolean>(`${BASE}/${enc(id)}`),
};

// ── BusinessPartner Roles ─────────────────────────────────────────────────────

export const bpRoleService = {
  /** GET /{bpId}/roles */
  list: (
    bpId: string,
    onlyActive?: boolean,
  ): Promise<BusinessPartnerRoleDto[]> => {
    const qs = onlyActive !== undefined ? `?onlyActive=${onlyActive}` : "";
    return apiGet<BusinessPartnerRoleDto[]>(`${BASE}/${enc(bpId)}/roles${qs}`);
  },

  /**
   * POST /{bpId}/roles — UPSERT semántico:
   *   - Si el rol no existe → crea
   *   - Si está revocado → reactiva
   *   - Si ya está activo → 422
   */
  assign: (
    bpId: string,
    body: AssignRoleBody,
  ): Promise<BusinessPartnerRoleDto> =>
    apiPost<BusinessPartnerRoleDto>(`${BASE}/${enc(bpId)}/roles`, body),

  /** DELETE /{bpId}/roles/{roleId} */
  revoke: (bpId: string, roleId: string): Promise<boolean> =>
    apiDelete<boolean>(`${BASE}/${enc(bpId)}/roles/${enc(roleId)}`),

  /** PATCH /{bpId}/roles/{roleId}/supplier-config */
  updateSupplierConfig: (
    bpId: string,
    roleId: string,
    config: SupplierConfigBody,
  ): Promise<BusinessPartnerRoleDto> =>
    apiPatch<BusinessPartnerRoleDto>(
      `${BASE}/${enc(bpId)}/roles/${enc(roleId)}/supplier-config`,
      config,
    ),

  /** PATCH /{bpId}/roles/{roleId}/carrier-config */
  updateCarrierConfig: (
    bpId: string,
    roleId: string,
    config: CarrierConfigBody,
  ): Promise<BusinessPartnerRoleDto> =>
    apiPatch<BusinessPartnerRoleDto>(
      `${BASE}/${enc(bpId)}/roles/${enc(roleId)}/carrier-config`,
      config,
    ),

  /** PATCH /{bpId}/roles/{roleId}/notes */
  updateNotes: (
    bpId: string,
    roleId: string,
    body: UpdateRoleNotesBody,
  ): Promise<boolean> =>
    apiPatch<boolean>(`${BASE}/${enc(bpId)}/roles/${enc(roleId)}/notes`, body),

  /** PATCH /{bpId}/roles/{roleId}/customer-config */
  updateCustomerConfig: (
    bpId: string,
    roleId: string,
    config: CustomerConfigBody,
  ): Promise<BusinessPartnerRoleDto> =>
    apiPatch<BusinessPartnerRoleDto>(
      `${BASE}/${enc(bpId)}/roles/${enc(roleId)}/customer-config`,
      config,
    ),

  /** PATCH /{bpId}/roles/{roleId}/supplier-classification */
  updateSupplierClassification: (
    bpId: string,
    roleId: string,
    config: SupplierClassificationBody,
  ): Promise<BusinessPartnerRoleDto> =>
    apiPatch<BusinessPartnerRoleDto>(
      `${BASE}/${enc(bpId)}/roles/${enc(roleId)}/supplier-classification`,
      config,
    ),
};

// ── BusinessPartner Locations ─────────────────────────────────────────────────

export const bpLocationService = {
  /** GET /{bpId}/locations */
  list: (bpId: string, onlyActive?: boolean): Promise<BpLocationDto[]> =>
    apiGet<BpLocationDto[]>(
      `${BASE}/${enc(bpId)}/locations${onlyActive !== undefined ? `?onlyActive=${onlyActive}` : ""}`,
    ),

  /** GET /{bpId}/locations/{id} */
  getById: (bpId: string, locationId: string): Promise<BpLocationDto> =>
    apiGet<BpLocationDto>(`${BASE}/${enc(bpId)}/locations/${enc(locationId)}`),

  /** POST /{bpId}/locations */
  create: (bpId: string, body: CreateLocationBody): Promise<BpLocationDto> =>
    apiPost<BpLocationDto>(`${BASE}/${enc(bpId)}/locations`, body),

  /** PUT /{bpId}/locations/{id} */
  update: (
    bpId: string,
    locationId: string,
    body: UpdateLocationBody,
  ): Promise<BpLocationDto> =>
    apiPut<BpLocationDto>(
      `${BASE}/${enc(bpId)}/locations/${enc(locationId)}`,
      body,
    ),

  /** PATCH /{bpId}/locations/{id}/set-primary */
  setPrimary: (bpId: string, locationId: string): Promise<boolean> =>
    apiPatch<boolean>(
      `${BASE}/${enc(bpId)}/locations/${enc(locationId)}/set-primary`,
    ),

  /** PATCH /{bpId}/locations/{id}/activate */
  activate: (bpId: string, locationId: string): Promise<boolean> =>
    apiPatch<boolean>(
      `${BASE}/${enc(bpId)}/locations/${enc(locationId)}/activate`,
    ),

  /** DELETE /{bpId}/locations/{id} */
  deactivate: (bpId: string, locationId: string): Promise<boolean> =>
    apiDelete<boolean>(`${BASE}/${enc(bpId)}/locations/${enc(locationId)}`),
};

// ── BusinessPartner Contacts ──────────────────────────────────────────────────

export const bpContactService = {
  /** GET /{bpId}/contacts */
  list: (bpId: string, onlyActive?: boolean): Promise<BpContactDto[]> =>
    apiGet<BpContactDto[]>(
      `${BASE}/${enc(bpId)}/contacts${onlyActive !== undefined ? `?onlyActive=${onlyActive}` : ""}`,
    ),

  /** GET /{bpId}/contacts/{id} */
  getById: (bpId: string, contactId: string): Promise<BpContactDto> =>
    apiGet<BpContactDto>(`${BASE}/${enc(bpId)}/contacts/${enc(contactId)}`),

  /** POST /{bpId}/contacts */
  create: (bpId: string, body: CreateContactBody): Promise<BpContactDto> =>
    apiPost<BpContactDto>(`${BASE}/${enc(bpId)}/contacts`, body),

  /** PUT /{bpId}/contacts/{id} */
  update: (
    bpId: string,
    contactId: string,
    body: UpdateContactBody,
  ): Promise<BpContactDto> =>
    apiPut<BpContactDto>(
      `${BASE}/${enc(bpId)}/contacts/${enc(contactId)}`,
      body,
    ),

  /** PATCH /{bpId}/contacts/{id}/set-primary */
  setPrimary: (bpId: string, contactId: string): Promise<boolean> =>
    apiPatch<boolean>(
      `${BASE}/${enc(bpId)}/contacts/${enc(contactId)}/set-primary`,
    ),

  /** PATCH /{bpId}/contacts/{id}/activate */
  activate: (bpId: string, contactId: string): Promise<boolean> =>
    apiPatch<boolean>(
      `${BASE}/${enc(bpId)}/contacts/${enc(contactId)}/activate`,
    ),

  /** DELETE /{bpId}/contacts/{id} */
  deactivate: (bpId: string, contactId: string): Promise<boolean> =>
    apiDelete<boolean>(`${BASE}/${enc(bpId)}/contacts/${enc(contactId)}`),
};

// ── CompanyBpTradingSettings ──────────────────────────────────────────────────

export const bpTradingSettingsService = {
  /** GET /{bpId}/trading-settings */
  get: (bpId: string): Promise<CompanyBpTradingSettingsDto> =>
    apiGet<CompanyBpTradingSettingsDto>(
      `${BASE}/${enc(bpId)}/trading-settings`,
    ),

  /** PUT /{bpId}/trading-settings — crea o actualiza */
  upsert: (
    bpId: string,
    body: UpsertTradingSettingsBody,
  ): Promise<CompanyBpTradingSettingsDto> =>
    apiPut<CompanyBpTradingSettingsDto>(
      `${BASE}/${enc(bpId)}/trading-settings`,
      body,
    ),

  /** PATCH /{bpId}/trading-settings/block */
  block: (bpId: string, body: BlockBody): Promise<boolean> =>
    apiPatch<boolean>(`${BASE}/${enc(bpId)}/trading-settings/block`, body),

  /** PATCH /{bpId}/trading-settings/unblock */
  unblock: (bpId: string): Promise<boolean> =>
    apiPatch<boolean>(`${BASE}/${enc(bpId)}/trading-settings/unblock`),
};
