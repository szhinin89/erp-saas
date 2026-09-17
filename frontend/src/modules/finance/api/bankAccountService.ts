import { apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";
import type { BankAccountTypeCode } from "../schemas/bankAccountSchema";

const BASE = "/api/v1/treasury/bank-accounts";

// ── DTOs (mismo contrato que CompanyBankAccountDto en ERP.API) ──────────

export interface CompanyBankAccountDto {
  id: string;
  bankId: string;
  accountType: BankAccountTypeCode;
  accountNumber: string;
  displayName: string;
  accountingAccountId: string;
  isActive: boolean;
}

export interface CreateBankAccountPayload {
  bankId: string;
  accountType: BankAccountTypeCode;
  accountNumber: string;
  displayName: string;
  accountingAccountId: string;
}

export interface UpdateBankAccountPayload {
  displayName: string;
  accountingAccountId: string;
}

/**
 * TREASURY-BANK-ACCOUNTS-01: consume exclusivamente los endpoints de
 * `CompanyBankAccountController` (ERP.API) — CRUD básico + activar/desactivar. Sin posting, sin
 * caja, sin conciliación, sin movimientos bancarios.
 */
export const bankAccountService = {
  list: (isActive?: boolean) => {
    const params = new URLSearchParams();
    if (isActive !== undefined) params.set("isActive", String(isActive));
    const qs = params.toString();
    return apiGet<CompanyBankAccountDto[]>(qs ? `${BASE}?${qs}` : BASE);
  },

  getById: (id: string) => apiGet<CompanyBankAccountDto>(`${BASE}/${id}`),

  create: (payload: CreateBankAccountPayload) =>
    apiPost<CompanyBankAccountDto>(BASE, payload),

  update: (id: string, payload: UpdateBankAccountPayload) =>
    apiPut<CompanyBankAccountDto>(`${BASE}/${id}`, payload),

  enable: (id: string) => apiPost<CompanyBankAccountDto>(`${BASE}/${id}/enable`, {}),

  disable: (id: string) => apiPost<CompanyBankAccountDto>(`${BASE}/${id}/disable`, {}),
};
