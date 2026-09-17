import { apiPost } from "../../lib/apiEnvelope";

// ── DTOs (espejo exacto de PaymentDto / PaymentApplicationLineDto — backend) ──

export interface PaymentApplicationLineDto {
  id: string;
  receivableId: string | null;
  payableId: string | null;
  installmentId: string | null;
  appliedAmount: number;
}

export interface PaymentDto {
  id: string;
  direction: string;
  partnerId: string;
  amount: number;
  paymentDate: string;
  paymentMethodId: string | null;
  reference: string | null;
  status: string;
  appliedAtUtc: string | null;
  reversedAtUtc: string | null;
  reverseReason: string | null;
  lines: PaymentApplicationLineDto[];
  createdAt: string;
  updatedAt: string | null;
  /** FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01 — cuenta bancaria usada, si se especificó. */
  companyBankAccountId: string | null;
  /** FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01 — caja usada, si se especificó. */
  cashRegisterId: string | null;
}

export interface PaymentApplicationLineInput {
  documentId: string;
  installmentId?: string | null;
  appliedAmount: number;
}

export interface RegisterCollectionPayload {
  customerId: string;
  amount: number;
  paymentDate: string;
  paymentMethodId?: string | null;
  reference?: string | null;
  lines: PaymentApplicationLineInput[];
  /** FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01 — cuenta bancaria opcional, excluyente con cashRegisterId. */
  companyBankAccountId?: string | null;
  /** FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01 — caja opcional, excluyente con companyBankAccountId. */
  cashRegisterId?: string | null;
}

const BASE = "/api/v1/finance";

/**
 * P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — liquidación de CxC. Consume
 * FinancePaymentsController, que delega íntegramente en RegisterCollectionCommand (Application) —
 * este servicio no calcula ni valida ninguna regla de negocio, solo transporta el payload.
 * PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — registerPayment (CxP contra AccountsPayable) se eliminó
 * junto con RegisterPaymentCommand/FinancePaymentsController's POST /payments (sin UI ni endpoint
 * activo desde PAYABLES-LEGACY-CLEANUP-13).
 */
export const paymentService = {
  registerCollection: (payload: RegisterCollectionPayload) =>
    apiPost<PaymentDto>(`${BASE}/collections`, payload),
};
