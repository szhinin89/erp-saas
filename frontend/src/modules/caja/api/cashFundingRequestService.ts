import { apiGet, apiPost } from "../../lib/apiEnvelope";

const BASE = "/api/v1/cash-funding-requests";

// ── DTOs (mismo contrato que CashFundingRequestListItemDto/CashFundingRequestDto en ERP.API) ──

/** Espejo exacto de CashFundingRequestStatus — backend. */
export type CashFundingRequestStatus = "Pending" | "Fulfilled" | "Rejected" | "Cancelled";

/** ZH-CASH-FUNDING-REQUEST-UI-FINAL-02E-EF — fila de la bandeja de caja y de "Mis solicitudes". */
export interface CashFundingRequestListItemDto {
  id: string;
  supplierId: string;
  supplierName: string;
  requestedAtUtc: string;
  requestedByUserId: string;
  requestedByName: string;
  cashRegisterId: string;
  cashRegisterName: string;
  cashAmount: number;
  totalAmount: number;
  status: CashFundingRequestStatus;
  resolvedAtUtc: string | null;
  resolvedByName: string | null;
  supplierPaymentId: string | null;
}

/** Origen de fondos del pago solicitado: la caja (`Cash`) o una cuenta bancaria (`Bank`). */
export interface CashFundingRequestSourceLine {
  kind: "Cash" | "Bank";
  paymentMethodId: string;
  paymentMethodName: string;
  cashRegisterId: string | null;
  cashRegisterName: string | null;
  companyBankAccountId: string | null;
  bankAccountName: string | null;
  amount: number;
  transactionDate: string | null;
  referenceNumber: string | null;
  checkNumber: string | null;
}

/** Cuota de CxP que el pago solicitado aplicaría. */
export interface CashFundingRequestApplicationLine {
  accountsPayableInstallmentId: string;
  accountsPayableId: string | null;
  documentNumber: string | null;
  /** "PurchaseInvoice" | "Expense" | … — etiqueta vía lib/payableOrigin. */
  originType: string | null;
  installmentNumber: number | null;
  amountApplied: number;
}

/**
 * Detalle. Nunca trae el payload guardado, su huella ni el ClientRequestId. `canFulfill`/
 * `canReject`/`canCancel` los deriva el servidor (permiso + ownership): la UI solo los obedece.
 */
export interface CashFundingRequestDto {
  id: string;
  status: CashFundingRequestStatus;
  branchId: string;
  branchName: string;
  cashRegisterId: string;
  cashRegisterName: string;
  cashSessionId: string;
  supplierId: string;
  supplierName: string;
  totalAmount: number;
  cashAmount: number;
  requestedByUserId: string;
  requestedByName: string;
  requestedAtUtc: string;
  resolvedByUserId: string | null;
  resolvedByName: string | null;
  resolvedAtUtc: string | null;
  resolutionReason: string | null;
  supplierPaymentId: string | null;
  paymentDate: string | null;
  receiptNumber: string | null;
  sources: CashFundingRequestSourceLine[];
  applications: CashFundingRequestApplicationLine[];
  canFulfill: boolean;
  canReject: boolean;
  canCancel: boolean;
}

/** PagedResult<T> del backend. */
export interface CashFundingRequestPage {
  items: CashFundingRequestListItemDto[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface CashFundingRequestListFilters {
  status?: CashFundingRequestStatus | null;
  cashRegisterId?: string | null;
}

/**
 * Cuerpo de creación: exactamente el mismo pago que POST /supplier-payments (RegisterSupplierPaymentRequest,
 * mismo contrato estructural) + la clave de idempotencia.
 */
export interface CreateCashFundingRequestPayload {
  supplierId: string;
  paymentDate: string;
  totalAmount: number;
  receiptNumber?: string | null;
  methodLines: {
    paymentMethodId: string;
    companyBankAccountId: string | null;
    cashRegisterId: string | null;
    amount: number;
    referenceNumber?: string | null;
    checkNumber?: string | null;
    checkDate?: string | null;
    notes?: string | null;
    transactionDate?: string | null;
  }[];
  applicationLines: { accountsPayableInstallmentId: string; amountApplied: number }[];
  allocations: { methodLineIndex: number; applicationLineIndex: number; amount: number }[];
  confirmUnappliedAmount?: boolean;
  clientRequestId: string;
}

const query = (params: Record<string, string | number | null | undefined>) => {
  const qs = new URLSearchParams();
  for (const [key, value] of Object.entries(params))
    if (value !== null && value !== undefined && value !== "") qs.set(key, String(value));
  return qs.toString();
};

// ── Service ──────────────────────────────────────────────────────────────

export const cashFundingRequestService = {
  /** Bandeja de caja (`caja.funding-requests.view`): sucursal activa, resuelta por el servidor. */
  list: (page: number, pageSize: number, filters: CashFundingRequestListFilters = {}) =>
    apiGet<CashFundingRequestPage>(
      `${BASE}?${query({ status: filters.status, cashRegisterId: filters.cashRegisterId, page, pageSize })}`,
    ),

  /** "Mis solicitudes": el servidor fija el solicitante al usuario autenticado. */
  listMine: (page: number, pageSize: number, status?: CashFundingRequestStatus | null) =>
    apiGet<CashFundingRequestPage>(`${BASE}/mine?${query({ status, page, pageSize })}`),

  getById: (id: string) => apiGet<CashFundingRequestDto>(`${BASE}/${id}`),

  create: (payload: CreateCashFundingRequestPayload) => apiPost<CashFundingRequestDto>(BASE, payload),

  fulfill: (id: string) => apiPost<CashFundingRequestDto>(`${BASE}/${id}/fulfill`, {}),

  reject: (id: string, reason: string) => apiPost<CashFundingRequestDto>(`${BASE}/${id}/reject`, { reason }),

  cancel: (id: string, reason: string) => apiPost<CashFundingRequestDto>(`${BASE}/${id}/cancel`, { reason }),
};
