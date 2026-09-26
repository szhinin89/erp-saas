import { apiGet, apiPost } from "../../lib/apiEnvelope";

const BASE = "/api/v1/supplier-payments";

/** Espejo exacto de SupplierPaymentStatus — backend (serializado en PascalCase, sin lowercase). */
export type SupplierPaymentStatus = "Confirmed" | "Reversed";

// ── Request (POST) ──────────────────────────────────────────────────────

/** Espejo exacto de SupplierPaymentMethodLineRequest — backend. */
export interface SupplierPaymentMethodLineRequest {
  paymentMethodId: string;
  companyBankAccountId: string | null;
  cashRegisterId: string | null;
  amount: number;
  referenceNumber?: string | null;
  checkNumber?: string | null;
  checkDate?: string | null;
  notes?: string | null;
  /** 02A — fecha efectiva real de la fuente bancaria ("YYYY-MM-DD"); null en fuentes de caja. */
  transactionDate?: string | null;
}

/** Espejo exacto de SupplierPaymentApplicationLineRequest — backend. */
export interface SupplierPaymentApplicationLineRequest {
  accountsPayableInstallmentId: string;
  amountApplied: number;
}

/** Espejo exacto de SupplierPaymentAllocationLineRequest — backend. */
export interface SupplierPaymentAllocationLineRequest {
  methodLineIndex: number;
  applicationLineIndex: number;
  amount: number;
}

/** Espejo exacto de RegisterSupplierPaymentRequest — backend. */
export interface RegisterSupplierPaymentRequest {
  supplierId: string;
  paymentDate: string;
  totalAmount: number;
  receiptNumber?: string | null;
  methodLines: SupplierPaymentMethodLineRequest[];
  applicationLines: SupplierPaymentApplicationLineRequest[];
  allocations: SupplierPaymentAllocationLineRequest[];
  /**
   * ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — confirmación explícita del usuario de que el
   * remanente no aplicado quedará como anticipo a favor del proveedor. El backend la revalida.
   */
  confirmUnappliedAmount?: boolean;
}

// ── DTOs (GET / respuesta de POST) ──────────────────────────────────────

/** Espejo exacto de SupplierPaymentMethodLineDto — backend. */
export interface SupplierPaymentMethodLineDto {
  id: string;
  paymentMethodId: string;
  companyBankAccountId: string | null;
  cashRegisterId: string | null;
  amount: number;
  referenceNumber: string | null;
  checkNumber: string | null;
  checkDate: string | null;
  notes: string | null;
  /** 02A — fecha efectiva de la fuente bancaria; null en fuentes de caja. */
  transactionDate: string | null;
  /** 02A — sesión/movimiento de caja vinculados (solo fuentes de efectivo físico). */
  cashSessionId: string | null;
  cashMovementId: string | null;
}

/**
 * Espejo exacto de SupplierPaymentApplicationLineDto — backend.
 * SUPPLIER-PAYMENT-DETAIL-APPLICATION-LINE-DISPLAY-NAMES-01 — los campos de proyección
 * (`documentNumber`/`installmentNumber`/`dueDate`/`issueDate`/`originType`) son de solo lectura,
 * resueltos por el backend contra la CxP dueña de la cuota — pueden venir `null` en el caso
 * excepcional de que la cuota ya no se pueda resolver (nunca rompe el detalle).
 */
export interface SupplierPaymentApplicationLineDto {
  id: string;
  accountsPayableInstallmentId: string;
  amountApplied: number;
  documentNumber: string | null;
  installmentNumber: number | null;
  dueDate: string | null;
  issueDate: string | null;
  originType: string | null;
}

/** Espejo exacto de SupplierPaymentAllocationLineDto — backend. */
export interface SupplierPaymentAllocationLineDto {
  id: string;
  supplierPaymentMethodLineId: string;
  supplierPaymentApplicationLineId: string;
  amount: number;
}

/** Espejo exacto de SupplierPaymentDto — backend. */
export interface SupplierPaymentDto {
  id: string;
  supplierId: string;
  branchId: string;
  paymentDate: string;
  totalAmount: number;
  systemNumber: string;
  receiptNumber: string | null;
  displayNumber: string;
  status: SupplierPaymentStatus;
  methodLines: SupplierPaymentMethodLineDto[];
  applicationLines: SupplierPaymentApplicationLineDto[];
  allocations: SupplierPaymentAllocationLineDto[];
  createdAt: string;
  /** Solo presentes cuando status === "Reversed". */
  reversedAtUtc?: string | null;
  reversedBy?: string | null;
  reverseReason?: string | null;
  /** 02B-FINAL — motivo estructurado de la reversa de fuentes bancarias (null si no aplica). */
  reversalBankReason?: SupplierPaymentBankReversalReason | null;
  /** 02B-FINAL — confirmación registrada de que el efectivo no se entregó (null si no aplica). */
  reversalCashNotDeliveredConfirmed?: boolean | null;
  /** 02C — derivados del backend (nunca persistidos): Σ aplicaciones y remanente (anticipo). */
  appliedAmount: number;
  unappliedAmount: number;
  /** 02C — crédito de proveedor (anticipo) originado por el remanente; null si no hubo remanente. */
  supplierCreditId?: string | null;
}

/** Espejo exacto de ReverseSupplierPaymentRequest — backend (POST /{id}/reverse). */
export interface ReverseSupplierPaymentRequest {
  reason: string;
  /** Obligatorio (true) si el pago tiene fuentes de caja. */
  cashNotDeliveredConfirmed?: boolean;
  /** Obligatorio si el pago tiene fuentes bancarias. */
  bankReversalReason?: SupplierPaymentBankReversalReason | null;
}

/**
 * ZH-SUPPLIER-PAYMENT-REVERSAL-SEMANTICS-02B-FINAL — espejo del enum interno fijo
 * `SupplierPaymentBankReversalReason` (no es un catálogo configurable). Todas significan que la
 * transferencia NUNCA se debitó: la reversa es corrección documental, no devolución de fondos.
 */
export type SupplierPaymentBankReversalReason = "NotExecuted" | "RejectedByBank" | "RegistrationError";

/** Espejo exacto de SupplierPaymentListItemDto — backend. */
export interface SupplierPaymentListItemDto {
  id: string;
  supplierId: string;
  supplierName: string;
  paymentDate: string;
  totalAmount: number;
  systemNumber: string;
  receiptNumber: string | null;
  displayNumber: string;
  status: SupplierPaymentStatus;
  createdAt: string;
}

export interface SupplierPaymentsListResponse {
  items: SupplierPaymentListItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface SupplierPaymentsListFilters {
  supplierId?: string | null;
  status?: SupplierPaymentStatus | "";
}

/**
 * ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — espejo de SupplierPaymentPolicyDto (GET /policy):
 * política de empresa "pagos sin CxP", expuesta por el propio módulo con permiso de registro.
 */
export interface SupplierPaymentPolicyDto {
  allowWithoutPayable: boolean;
}

export const supplierPaymentService = {
  register: (payload: RegisterSupplierPaymentRequest) =>
    apiPost<SupplierPaymentDto>(BASE, payload),

  getPolicy: () => apiGet<SupplierPaymentPolicyDto>(`${BASE}/policy`),

  list: (filters: SupplierPaymentsListFilters, page = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (filters.supplierId) params.set("supplierId", filters.supplierId);
    if (filters.status) params.set("status", filters.status);
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    return apiGet<SupplierPaymentsListResponse>(`${BASE}?${params}`);
  },

  getById: (id: string) => apiGet<SupplierPaymentDto>(`${BASE}/${id}`),

  reverse: (id: string, request: ReverseSupplierPaymentRequest) =>
    apiPost<SupplierPaymentDto>(`${BASE}/${id}/reverse`, request satisfies ReverseSupplierPaymentRequest),
};
