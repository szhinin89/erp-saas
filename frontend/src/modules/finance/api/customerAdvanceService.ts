import { apiGet, apiPost } from "../../lib/apiEnvelope";

// ── DTOs (mismo contrato que CustomerAdvanceDto en ERP.API) ─────────────

export type CustomerAdvanceStatus = "Confirmed" | "Cancelled";

export type CustomerAdvanceMovementType =
  | "Received"
  | "Application"
  | "ReversalOfApplication"
  | "Refund"
  | "ReversalOfRefund"
  | "SourceCancelled"
  | "IncomeReversed";

export interface CustomerAdvanceListItemDto {
  id: string;
  customerId: string;
  customerName: string | null;
  branchId: string;
  advanceDate: string;
  currencyCode: string;
  originalAmount: number;
  availableAmount: number;
  status: CustomerAdvanceStatus;
  paymentMethodId: string;
  companyBankAccountId: string | null;
  cashRegisterId: string | null;
  externalReference: string | null;
}

export interface CustomerAdvanceMovementDto {
  id: string;
  movementType: CustomerAdvanceMovementType;
  amount: number;
  lineNumber: number;
  /** Operación a la que pertenece; identifica una aplicación a revertir. */
  operationId: string;
  branchId: string;
  effectiveDate: string;
  evidenceDate: string | null;
  reason: string | null;
  reference: string | null;
  receivableId: string | null;
  installmentId: string | null;
  reversalOfMovementId: string | null;
  cashSessionId: string | null;
  cashMovementId: string | null;
  createdByUserId: string;
  createdAtUtc: string;
}

export interface CustomerAdvanceRefundTransactionDto {
  id: string;
  movementId: string;
  transactionType: "Refund" | "RefundReversed";
  originalTransactionId: string | null;
  paymentMethodId: string;
  companyBankAccountId: string | null;
  cashRegisterId: string | null;
  amount: number;
  effectiveDate: string;
  reference: string;
  reason: string;
  cashSessionId: string | null;
  cashMovementId: string | null;
  createdByUserId: string;
  createdAtUtc: string;
  isReversed: boolean;
}

export interface CustomerAdvanceDto extends CustomerAdvanceListItemDto {
  note: string | null;
  movements: CustomerAdvanceMovementDto[];
  refundTransactions: CustomerAdvanceRefundTransactionDto[];
}

export interface CustomerAdvanceReceivableApplicationDto {
  customerAdvanceId: string;
  operationId: string;
  movementId: string;
  installmentId: string | null;
  amount: number;
  effectiveDate: string;
  branchId: string;
  createdByUserId: string;
  createdAtUtc: string;
  isReversed: boolean;
  reversalMovementId: string | null;
  reversalDate: string | null;
}

export interface CustomerAdvanceBalanceDto {
  customerId: string;
  currencyCode: string;
  availableAmount: number;
}

export interface CustomerAdvanceListResultDto {
  items: CustomerAdvanceListItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CustomerAdvanceListFilters {
  customerId?: string | null;
  status?: CustomerAdvanceStatus | "";
  branchId?: string | null;
}

// ── Payloads ────────────────────────────────────────────────────────────

interface MoneyTargetPayload {
  paymentMethodId: string;
  companyBankAccountId: string | null;
  cashRegisterId: string | null;
}

export interface RegisterCustomerAdvancePayload extends MoneyTargetPayload {
  customerId: string;
  amount: number;
  advanceDate: string;
  reference: string | null;
  reason: string | null;
  clientRequestId: string;
}

export interface ApplyCustomerAdvancePayload {
  effectiveDate: string;
  lines: { receivableId: string; installmentId: string | null; amount: number }[];
  clientRequestId: string;
}

export interface RefundCustomerAdvancePayload extends MoneyTargetPayload {
  amount: number;
  effectiveDate: string;
  reference: string;
  reason: string;
  clientRequestId: string;
}

export interface CompensationPayload {
  reason: string;
  reference: string;
  clientRequestId: string;
}

export interface EvidencedCompensationPayload extends CompensationPayload {
  evidenceDate: string;
}

export interface CancelCustomerAdvancePayload extends CompensationPayload {
  moneyNotReceived: boolean;
}

const BASE = "/api/v1/finance/customer-advances";

export const customerAdvanceService = {
  list: (page: number, pageSize: number, filters: CustomerAdvanceListFilters = {}) => {
    const params = new URLSearchParams();
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    if (filters.customerId) params.set("customerId", filters.customerId);
    if (filters.status) params.set("status", filters.status);
    if (filters.branchId) params.set("branchId", filters.branchId);
    return apiGet<CustomerAdvanceListResultDto>(`${BASE}?${params.toString()}`);
  },

  getById: (id: string) => apiGet<CustomerAdvanceDto>(`${BASE}/${id}`),

  balance: (customerId: string) =>
    apiGet<CustomerAdvanceBalanceDto>(`${BASE}/balance?customerId=${customerId}`),

  byReceivable: (receivableId: string) =>
    apiGet<CustomerAdvanceReceivableApplicationDto[]>(`${BASE}/by-receivable/${receivableId}`),

  register: (payload: RegisterCustomerAdvancePayload) => apiPost<CustomerAdvanceDto>(BASE, payload),

  apply: (id: string, payload: ApplyCustomerAdvancePayload) =>
    apiPost<CustomerAdvanceDto>(`${BASE}/${id}/applications`, payload),

  reverseApplication: (id: string, operationId: string, payload: CompensationPayload) =>
    apiPost<CustomerAdvanceDto>(`${BASE}/${id}/applications/${operationId}/reverse`, payload),

  refund: (id: string, payload: RefundCustomerAdvancePayload) =>
    apiPost<CustomerAdvanceDto>(`${BASE}/${id}/refunds`, payload),

  reverseRefund: (id: string, transactionId: string, payload: EvidencedCompensationPayload) =>
    apiPost<CustomerAdvanceDto>(`${BASE}/${id}/refunds/${transactionId}/reverse`, payload),

  reverseIncome: (id: string, payload: EvidencedCompensationPayload) =>
    apiPost<CustomerAdvanceDto>(`${BASE}/${id}/reverse-income`, payload),

  cancel: (id: string, payload: CancelCustomerAdvancePayload) =>
    apiPost<CustomerAdvanceDto>(`${BASE}/${id}/cancel`, payload),
};
