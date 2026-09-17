import { apiGet, apiPatch, apiPost, apiPut } from "../../../lib/apiEnvelope";

const BASE = "/api/v1/settings/catalogs/banks";

// ── DTOs ─────────────────────────────────────────────────────────────────

export interface BankDto {
  id: string;
  countryCode: string;
  code: string;
  name: string;
  shortName: string | null;
  isActive: boolean;
}

// ── Payloads ─────────────────────────────────────────────────────────────

export interface CreateBankPayload {
  code: string;
  name: string;
  shortName?: string | null;
  countryCode?: string;
}

export interface UpdateBankPayload {
  id: string;
  name: string;
  shortName?: string | null;
}

export const bankService = {
  list: (onlyActive?: boolean, search?: string) => {
    const params = new URLSearchParams();
    if (onlyActive !== undefined) params.set("onlyActive", String(onlyActive));
    if (search?.trim()) params.set("search", search.trim());
    const qs = params.toString();
    return apiGet<BankDto[]>(`${BASE}${qs ? `?${qs}` : ""}`);
  },
  getById: (id: string) => apiGet<BankDto>(`${BASE}/${id}`),
  create: (p: CreateBankPayload) => apiPost<BankDto>(BASE, p),
  update: (id: string, p: UpdateBankPayload) => apiPut<BankDto>(`${BASE}/${id}`, p),
  enable: (id: string) => apiPatch<boolean>(`${BASE}/${id}/enable`),
  disable: (id: string) => apiPatch<boolean>(`${BASE}/${id}/disable`),
};
