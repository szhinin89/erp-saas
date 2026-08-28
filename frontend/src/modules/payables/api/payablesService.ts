import { apiGet } from "../../lib/apiEnvelope";

const BASE = "/api/v1/payables";

/** Espejo exacto de AccountsPayableOriginType — backend. */
export type PayableOriginType = "PurchaseInvoice" | "ExpenseDocument" | "Manual";

/** Espejo exacto de AccountsPayableStatus — backend (serializado en minusculas). */
export type PayableStatus = "pending" | "partiallypaid" | "paid" | "cancelled";

/** Espejo exacto de AccountsPayableListItemDto — backend. */
export interface PayableListItemDto {
  id: string;
  supplierId: string;
  supplierName: string;
  originType: PayableOriginType;
  originId: string;
  documentType: string;
  documentNumber: string;
  issueDate: string;
  dueDate: string | null;
  totalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  status: PayableStatus;
}

export interface PayablesListResponse {
  items: PayableListItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

/** Espejo exacto de AccountsPayableInstallmentDetailDto — backend. */
export interface PayableInstallmentDto {
  installmentId: string;
  installmentNumber: number;
  dueDate: string;
  amount: number;
  paidAmount: number;
  outstandingAmount: number;
  status: PayableStatus;
}

/** Espejo exacto de AccountsPayableDetailDto — backend. */
export interface PayableDetailDto {
  id: string;
  supplierId: string;
  supplierName: string;
  originType: PayableOriginType;
  originId: string;
  documentType: string;
  documentNumber: string;
  issueDate: string;
  accountingDate: string;
  totalAmount: number;
  paidAmount: number;
  retainedAmount: number;
  returnCreditAmount: number;
  supplierCreditAmount: number;
  creditNoteAmount: number;
  outstandingAmount: number;
  status: PayableStatus;
  installments: PayableInstallmentDto[];
  createdAt: string;
  updatedAt: string | null;
}

export interface PayablesListFilters {
  supplierId?: string | null;
  originType?: PayableOriginType | "";
  status?: PayableStatus | "";
  dueDateFrom?: string;
  dueDateTo?: string;
  search?: string;
}

export const payablesService = {
  list: (filters: PayablesListFilters, page = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (filters.supplierId) params.set("supplierId", filters.supplierId);
    if (filters.originType) params.set("originType", filters.originType);
    if (filters.status) params.set("status", filters.status);
    if (filters.dueDateFrom) params.set("dueDateFrom", filters.dueDateFrom);
    if (filters.dueDateTo) params.set("dueDateTo", filters.dueDateTo);
    if (filters.search?.trim()) params.set("search", filters.search.trim());
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    return apiGet<PayablesListResponse>(`${BASE}?${params}`);
  },

  getById: (id: string) => apiGet<PayableDetailDto>(`${BASE}/${id}`),
};
