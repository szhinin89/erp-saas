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
  /** ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — destino financiero (caja/banco) usado, si se especificó. */
  financialDestinationId: string | null;
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
  /** ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — destino financiero (caja/banco) opcional. */
  financialDestinationId?: string | null;
}

export interface RegisterPaymentPayload {
  supplierId: string;
  amount: number;
  paymentDate: string;
  paymentMethodId?: string | null;
  reference?: string | null;
  lines: PaymentApplicationLineInput[];
  /** ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — destino financiero (caja/banco) opcional. */
  financialDestinationId?: string | null;
}

const BASE = "/api/v1/finance";

/**
 * P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — liquidación de CxC/CxP. Consume
 * FinancePaymentsController, que delega íntegramente en RegisterCollectionCommand/
 * RegisterPaymentCommand (Application) ya existentes — este servicio no calcula ni valida
 * ninguna regla de negocio, solo transporta el payload.
 */
export const paymentService = {
  registerCollection: (payload: RegisterCollectionPayload) =>
    apiPost<PaymentDto>(`${BASE}/collections`, payload),

  registerPayment: (payload: RegisterPaymentPayload) =>
    apiPost<PaymentDto>(`${BASE}/payments`, payload),
};
