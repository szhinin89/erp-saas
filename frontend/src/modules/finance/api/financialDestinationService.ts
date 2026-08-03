import { apiGet, apiPost } from "../../lib/apiEnvelope";

const BASE = "/api/v1/finance/financial-destinations";

export type FinancialDestinationTypeCode = "BankAccount" | "CashRegister";

// ── DTOs (mismo contrato que CompanyFinancialDestinationDto en ERP.API) ──

export interface CompanyFinancialDestinationDto {
  id: string;
  code: string;
  name: string;
  destinationTypeCode: FinancialDestinationTypeCode;
  accountingAccountId: string;
  currencyCode: string;
  cashRegisterId: string | null;
  bankInstitutionCode: string | null;
  bankAccountIdentifierNormalized: string | null;
  isActive: boolean;
}

// ── Payloads — los 8 campos estructurales solo se envían en Create; ningún
// otro caso de uso los acepta (§6.4ter, inmutables tras la creación). ──────

export interface CreateFinancialDestinationPayload {
  code: string;
  name: string;
  destinationTypeCode: FinancialDestinationTypeCode;
  accountingAccountId: string;
  currencyCode: string;
  cashRegisterId?: string | null;
  bankInstitutionCode?: string | null;
  bankAccountIdentifierNormalized?: string | null;
}

// ── Service ─────────────────────────────────────────────────────────────

/**
 * Consume exclusivamente los 5 endpoints ya implementados de
 * `CompanyFinancialDestinationController` (ERP.API, Fase 4 + Remediación 01 de Fase 13) — sin
 * lógica de negocio propia. Sin `update`/`delete` genéricos: solo los 3 casos de uso de mutación
 * limitada que el backend expone (rename, cambiar cuenta contable, activar/desactivar).
 */
export const financialDestinationService = {
  list: (isActive?: boolean) => {
    const params = new URLSearchParams();
    if (isActive !== undefined) params.set("isActive", String(isActive));
    const qs = params.toString();
    return apiGet<CompanyFinancialDestinationDto[]>(qs ? `${BASE}?${qs}` : BASE);
  },

  create: (payload: CreateFinancialDestinationPayload) =>
    apiPost<CompanyFinancialDestinationDto>(BASE, payload),

  rename: (id: string, name: string) =>
    apiPost<CompanyFinancialDestinationDto>(`${BASE}/${id}/rename`, { name }),

  changeAccountingAccount: (id: string, accountingAccountId: string) =>
    apiPost<CompanyFinancialDestinationDto>(`${BASE}/${id}/change-accounting-account`, {
      accountingAccountId,
    }),

  setActive: (id: string, isActive: boolean) =>
    apiPost<CompanyFinancialDestinationDto>(`${BASE}/${id}/set-active`, { isActive }),
};
