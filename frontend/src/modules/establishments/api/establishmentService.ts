import { apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';

// ── DTOs ──────────────────────────────────────────────────────────────────

export type EstablishmentListItemDto = {
  id: string;
  code: string;
  name: string;
  address: string;
  phone: string | null;
  branchId: string | null;
  branchName: string | null;
  emissionPointCount: number;
  isMain: boolean;
  isActive: boolean;
  createdAt: string;
};

export type EstablishmentDto = {
  id: string;
  branchId: string | null;
  companyId: string;
  code: string;
  name: string;
  address: string;
  phone: string | null;
  isMain: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
};

export type EstablishmentLookupDto = {
  id: string;
  code: string;
  name: string;
};

// ── Payloads ──────────────────────────────────────────────────────────────

export type CreateEstablishmentPayload = {
  branchId?: string | null;
  code: string;
  name: string;
  address: string;
  phone?: string | null;
  isMain: boolean;
};

export type UpdateEstablishmentPayload = {
  id: string;
  name: string;
  address: string;
  phone?: string | null;
  isMain: boolean;
};

// ── Service ───────────────────────────────────────────────────────────────

const BASE = '/api/v1/settings/establishments';

export const establishmentService = {
  list: (activeStatus: 'all' | 'active' | 'inactive' = 'active', branchId?: string, search?: string) => {
    const params = new URLSearchParams({ activeStatus });
    if (branchId) params.set('branchId', branchId);
    if (search)   params.set('search', search);
    return apiGet<EstablishmentListItemDto[]>(`${BASE}?${params.toString()}`);
  },

  lookups: () =>
    apiGet<EstablishmentLookupDto[]>(`${BASE}/lookups`),

  create: (body: CreateEstablishmentPayload) =>
    apiPost<EstablishmentDto>(BASE, body),

  update: (id: string, body: UpdateEstablishmentPayload) =>
    apiPut<EstablishmentDto>(`${BASE}/${id}`, body),

  disable: (id: string) =>
    apiPatch<boolean>(`${BASE}/${id}/disable`),

  enable: (id: string) =>
    apiPatch<boolean>(`${BASE}/${id}/enable`),
};
