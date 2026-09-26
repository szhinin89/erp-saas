import { apiGet, apiPost } from "../../lib/apiEnvelope";

const BASE = "/api/v1/finance/supplier-credits";

// ── DTOs (mismo contrato que SupplierCreditDto/SupplierCreditRefundTransactionDto en ERP.API) ──

/**
 * ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — movimiento del historial con los datos que el dominio
 * guarda según su tipo (null cuando no aplican): aplicación → CxP destino; reembolso → destino,
 * forma de pago, referencia; reversas → movimiento original / motivo.
 */
export interface SupplierCreditMovementDto {
  id: string;
  movementType: string;
  amount: number;
  createdAtUtc: string;
  createdByUserId: string;
  createdByName: string | null;
  reversalOfMovementId: string | null;
  reversedByMovementId: string | null;
  accountsPayableId: string | null;
  payableDocumentNumber: string | null;
  payableOriginType: string | null;
  refundTransactionId: string | null;
  effectiveDate: string | null;
  destinationType: "Cash" | "Bank" | null;
  destinationName: string | null;
  paymentMethodCode: string | null;
  referenceNumber: string | null;
  reason: string | null;
}

/** Detalle de un saldo a favor (GET /{id} y respuesta de aplicar/reversar aplicación). */
export interface SupplierCreditDto {
  id: string;
  supplierId: string;
  supplierName: string | null;
  branchId: string;
  currencyCode: string;
  /** Origen (exactamente uno): devolución de compra o remanente no aplicado de un pago (anticipo). */
  sourceType: SupplierCreditSourceType;
  sourceDocumentId: string;
  sourcePurchaseReturnId: string | null;
  sourceSupplierPaymentId: string | null;
  sourceDocumentNumber: string | null;
  sourceDate: string | null;
  originalAmount: number;
  availableAmount: number;
  isOpen: boolean;
  movements: SupplierCreditMovementDto[];
}

/** ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — fila del listado. */
export interface SupplierCreditListItemDto {
  id: string;
  supplierId: string;
  supplierName: string | null;
  sourceType: SupplierCreditSourceType;
  sourceDocumentId: string;
  sourceDocumentNumber: string | null;
  sourceDate: string | null;
  currencyCode: string;
  originalAmount: number;
  availableAmount: number;
  isOpen: boolean;
}

/** Filtros server-side opcionales del listado (02D-D). */
export interface SupplierCreditListFilters {
  supplierId?: string | null;
  sourceType?: SupplierCreditSourceType | null;
  isOpen?: boolean | null;
}

/** Espejo exacto de SupplierCreditSourceType — backend (derivado, nunca persistido). */
export type SupplierCreditSourceType = "PurchaseReturn" | "SupplierPayment";


export interface SupplierCreditListResultDto {
  items: SupplierCreditListItemDto[];
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
  list: (page = 1, pageSize = 25, filters: SupplierCreditListFilters = {}) => {
    const params = new URLSearchParams();
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    if (filters.supplierId) params.set("supplierId", filters.supplierId);
    if (filters.sourceType) params.set("sourceType", filters.sourceType);
    if (filters.isOpen !== undefined && filters.isOpen !== null) params.set("isOpen", String(filters.isOpen));
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
