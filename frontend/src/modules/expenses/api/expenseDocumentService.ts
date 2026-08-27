import { apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";

const BASE = "/api/v1/expenses/documents";

export type ExpenseStatus = "Draft" | "Confirmed" | "Cancelled";

export interface ExpenseLineDto {
  id: string;
  expenseSubcategoryId: string;
  snapshotAccountingAccountId: string;
  snapshotAccountingAccountCode: string | null;
  snapshotAccountingAccountName: string | null;
  description: string;
  quantity: number;
  unitAmount: number;
  discountAmount: number;
  vatCode: string;
  vatRate: number;
  vatAmount: number;
  taxInclusiveTotal: number;
  sortOrder: number;
  notes: string | null;
}

export interface ExpenseDocumentListItemDto {
  id: string;
  companyId: string;
  branchId: string;
  supplierId: string;
  supplierName: string;
  supplierTaxId: string;
  issueDate: string;
  accountingDate: string;
  documentType: string;
  documentNumber: string;
  dueDate: string | null;
  status: ExpenseStatus;
  lineCount: number;
  subtotal: number;
  totalDiscount: number;
  totalTax: number;
  grandTotal: number;
  createdAt: string;
}

export interface ExpenseDocumentListResponse {
  items: ExpenseDocumentListItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ExpenseDocumentDetailDto {
  id: string;
  companyId: string;
  branchId: string;
  supplierId: string;
  supplierName: string;
  supplierTaxId: string;
  issueDate: string;
  accountingDate: string;
  documentType: string;
  documentNumber: string;
  authorizationNumber: string | null;
  authorizationDate: string | null;
  paymentTermId: string;
  paymentTermName: string;
  dueDate: string | null;
  subtotal: number;
  totalDiscount: number;
  totalTax: number;
  grandTotal: number;
  notes: string | null;
  status: ExpenseStatus;
  lines: ExpenseLineDto[];
}

export interface ExpenseDraftLineRequest {
  expenseSubcategoryId: string;
  description?: string | null;
  quantity: number;
  unitPrice: number;
  discountValue?: number;
  vatCode?: string;
  notes?: string | null;
}

export interface CreateExpenseDraftPayload {
  supplierId: string;
  issueDate: string;
  accountingDate: string;
  documentType: string;
  documentNumber: string;
  paymentTermId?: string | null;
  dueDate?: string | null;
  lines: ExpenseDraftLineRequest[];
  authorizationNumber?: string | null;
  authorizationDate?: string | null;
  notes?: string | null;
}

export type UpdateExpenseDraftPayload = CreateExpenseDraftPayload;

export const expenseDocumentService = {
  list: (search?: string, status?: string, page = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (search?.trim()) params.set("search", search.trim());
    if (status?.trim()) params.set("status", status.trim());
    params.set("pageNumber", String(page));
    params.set("pageSize", String(pageSize));
    return apiGet<ExpenseDocumentListResponse>(`${BASE}?${params}`);
  },

  getById: (id: string) => apiGet<ExpenseDocumentDetailDto>(`${BASE}/${id}`),

  create: (payload: CreateExpenseDraftPayload) =>
    apiPost<ExpenseDocumentDetailDto>(BASE, payload),

  update: (id: string, payload: UpdateExpenseDraftPayload) =>
    apiPut<ExpenseDocumentDetailDto>(`${BASE}/${id}`, payload),

  confirm: (id: string) => apiPost<ExpenseDocumentDetailDto>(`${BASE}/${id}/confirm`, {}),
};
