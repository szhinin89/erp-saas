import { apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';

const BASE = '/api/v1/finance/credit-terms';

// ── DTOs ─────────────────────────────────────────────────────────────────

export interface CreditInstallmentDto {
  id: string;
  installmentNumber: number;
  daysOffset: number;
  percentage: number;
}

export interface CreditTermDto {
  id: string;
  code: string;
  name: string;
  mode: string;
  totalDays: number;
  isActive: boolean;
  installments: CreditInstallmentDto[];
  createdAt: string;
  updatedAt: string | null;
}

// ── Payloads ─────────────────────────────────────────────────────────────

export interface InstallmentInput {
  number: number;
  daysOffset: number;
  percentage: number;
}

export interface CreateCreditTermPayload {
  code: string;
  name: string;
  mode: number;
  totalDays: number;
  installments?: InstallmentInput[];
}

export interface UpdateCreditTermPayload {
  id: string;
  name: string;
  mode: number;
  totalDays: number;
  installments?: InstallmentInput[];
}

// ── Constants ────────────────────────────────────────────────────────────

export const CREDIT_TERM_MODES = [
  { value: 1, label: 'Operativo', desc: 'Plazo simple (ej: Contado, Net 30)' },
  { value: 2, label: 'Financiero Estricto', desc: 'Cuotas auditables que suman 100%' },
] as const;

export function creditTermModeName(mode: string | number): string {
  const m = typeof mode === 'string' ? parseInt(mode, 10) : mode;
  return CREDIT_TERM_MODES.find(p => p.value === m)?.label ?? mode.toString();
}

// ── Service ──────────────────────────────────────────────────────────────

export const creditTermService = {
  list: (isActive?: boolean, search?: string) => {
    const params = new URLSearchParams();
    if (isActive !== undefined) params.set('isActive', String(isActive));
    if (search?.trim()) params.set('search', search.trim());
    const qs = params.toString();
    return apiGet<CreditTermDto[]>(`${BASE}${qs ? `?${qs}` : ''}`);
  },
  getById: (id: string) => apiGet<CreditTermDto>(`${BASE}/${id}`),
  create:  (p: CreateCreditTermPayload) => apiPost<CreditTermDto>(BASE, p),
  update:  (id: string, p: UpdateCreditTermPayload) => apiPut<CreditTermDto>(`${BASE}/${id}`, p),
  enable:  (id: string) => apiPatch<boolean>(`${BASE}/${id}/enable`),
  disable: (id: string) => apiPatch<boolean>(`${BASE}/${id}/disable`),
};
