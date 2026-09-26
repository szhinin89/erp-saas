import { apiGet, apiPost } from "../../lib/apiEnvelope";

const BASE = "/api/v1/finance/supplier-credits";

// ── DTOs (mismo contrato que SupplierCreditDto/SupplierCreditRefundTransactionDto en ERP.API) ──

export interface SupplierCreditMovementDto {
  id: string;
  movementType: string;
  amount: number;
  targetPurchasePayableId: string | null;
  reversalOfMovementId: string | null;
  createdAtUtc: string;
}

export interface SupplierCreditDto {
  id: string;
  supplierId: string;
  branchId: string;
  currencyCode: string;
  /**
   * ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — origen generalizado (exactamente uno):
   * devolución de compra o remanente no aplicado de un pago a proveedor (anticipo).
   */
  sourceType: SupplierCreditSourceType;
  sourcePurchaseReturnId: string | null;
  sourceSupplierPaymentId: string | null;
  /** Número visible del documento de origen (solo en consultas; null en respuestas de comandos). */
  sourceDocumentNumber: string | null;
  originalAmount: number;
  availableAmount: number;
  isOpen: boolean;
  movements: SupplierCreditMovementDto[];
}

/** Espejo exacto de SupplierCreditSourceType — backend (derivado, nunca persistido). */
export type SupplierCreditSourceType = "PurchaseReturn" | "SupplierPayment";


export interface SupplierCreditListResultDto {
  items: SupplierCreditDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface SupplierCreditRefundTransactionDto {
  id: string;
  transactionTypeCode: string;
  originalTransactionId: string | null;
  companyBankAccountId: string | null;
  cashRegisterId: string | null;
  accountingAccountId: string;
  paymentMethodCode: string;
  amount: number;
  currencyCode: string;
  effectiveDate: string;
  externalReference: string | null;
  reason: string | null;
  cashSessionId: string | null;
  cashMovementId: string | null;
}

// ── Payloads ────────────────────────────────────────────────────────────

export interface ApplySupplierCreditPayload {
  targetPurchasePayableId: string;
  amount: number;
  clientRequestId: string;
}

export interface ReverseSupplierCreditApplicationPayload {
  targetPurchasePayableId: string;
  clientRequestId: string;
}

export interface RegisterSupplierCreditRefundPayload {
  companyBankAccountId: string | null;
  cashRegisterId: string | null;
  paymentMethodCode: string;
  amount: number;
  effectiveDate: string;
  externalReference: string | null;
  clientRequestId: string;
}

export interface ReverseSupplierCreditRefundPayload {
  reason: string;
  effectiveDate: string;
  clientRequestId: string;
}

// ── Service ─────────────────────────────────────────────────────────────

/**
 * Consume exclusivamente los 6 endpoints ya implementados de
 * `SupplierCreditController` (ERP.API, Fase 11) — sin lógica de negocio propia. `AvailableAmount`
 * mostrado es siempre el valor cacheado del servidor — nunca se recalcula en el cliente (§4.2 del
 * diseño, ver plan Fase 13 cambio exacto #1).
 */
export const supplierCreditService = {
  list: (page = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    return apiGet<SupplierCreditListResultDto>(`${BASE}?${params}`);
  },

  getById: (id: string) => apiGet<SupplierCreditDto>(`${BASE}/${id}`),

  apply: (id: string, payload: ApplySupplierCreditPayload) =>
    apiPost<SupplierCreditDto>(`${BASE}/${id}/apply`, payload),

  reverseApplication: (
    id: string,
    movementId: string,
    payload: ReverseSupplierCreditApplicationPayload,
  ) => apiPost<SupplierCreditDto>(`${BASE}/${id}/apply/${movementId}/reverse`, payload),

  registerRefund: (id: string, payload: RegisterSupplierCreditRefundPayload) =>
    apiPost<SupplierCreditRefundTransactionDto>(`${BASE}/${id}/refund`, payload),

  reverseRefund: (
    id: string,
    movementId: string,
    payload: ReverseSupplierCreditRefundPayload,
  ) =>
    apiPost<SupplierCreditRefundTransactionDto>(
      `${BASE}/${id}/refund/${movementId}/reverse`,
      payload,
    ),
};
