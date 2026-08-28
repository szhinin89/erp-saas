import { apiGet, apiPost } from "../../lib/apiEnvelope";

const BASE = "/api/v1/supplier-payments";

/** Espejo exacto de SupplierPaymentStatus — backend (serializado en PascalCase, sin lowercase). */
export type SupplierPaymentStatus = "Confirmed" | "Reversed";

// ── Request (POST) ──────────────────────────────────────────────────────

/** Espejo exacto de SupplierPaymentMethodLineRequest — backend. */
export interface SupplierPaymentMethodLineRequest {
  paymentMethodId: string;
  financialDestinationId: string;
  amount: number;
  referenceNumber?: string | null;
  checkNumber?: string | null;
  checkDate?: string | null;
  notes?: string | null;
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
}

// ── DTOs (GET / respuesta de POST) ──────────────────────────────────────

/** Espejo exacto de SupplierPaymentMethodLineDto — backend. */
export interface SupplierPaymentMethodLineDto {
  id: string;
  paymentMethodId: string;
  financialDestinationId: string;
  amount: number;
  referenceNumber: string | null;
  checkNumber: string | null;
  checkDate: string | null;
  notes: string | null;
}

/** Espejo exacto de SupplierPaymentApplicationLineDto — backend. */
export interface SupplierPaymentApplicationLineDto {
  id: string;
  accountsPayableInstallmentId: string;
  amountApplied: number;
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
}

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

export const supplierPaymentService = {
  register: (payload: RegisterSupplierPaymentRequest) =>
    apiPost<SupplierPaymentDto>(BASE, payload),

  list: (filters: SupplierPaymentsListFilters, page = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (filters.supplierId) params.set("supplierId", filters.supplierId);
    if (filters.status) params.set("status", filters.status);
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    return apiGet<SupplierPaymentsListResponse>(`${BASE}?${params}`);
  },

  getById: (id: string) => apiGet<SupplierPaymentDto>(`${BASE}/${id}`),
};
