import { apiDelete, apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";

const BASE = "/api/v1/sales/returns";

// ── DTOs (mismo contrato que SalesReturnDto/ReturnableLineDto en ERP.API) ──

export interface SalesReturnDetailDto {
  id: string;
  originalInvoiceDetailId: string;
  itemId: string | null;
  description: string;
  snapshotSku: string | null;
  snapshotItemName: string | null;
  warehouseId: string | null;
  uomCode: string;
  quantity: number;
  unitPrice: number;
  discountPct: number;
  discountAmount: number;
  vatCode: string;
  vatRate: number;
  vatAmount: number;
  iceCode: string | null;
  iceRate: number;
  iceAmount: number;
  lineSubtotal: number;
  taxableBase: number;
  taxInclusiveTotal: number;
  isFrozen: boolean;
}

export type SalesReturnRefundMethod = "Cash" | "ReceivableCredit";

export interface SalesReturnRefundAllocationDto {
  id: string;
  method: SalesReturnRefundMethod;
  amount: number;
}

export interface SalesReturnDto {
  id: string;
  salesInvoiceId: string;
  customerId: string;
  returnNumber: string;
  reason: string;
  status: string;
  subtotal: number;
  totalVat: number;
  totalIce: number;
  totalDiscount: number;
  grandTotal: number;
  lines: SalesReturnDetailDto[];
  refundAllocations: SalesReturnRefundAllocationDto[];
  createdAt: string;
  updatedAt: string | null;
}

export interface SalesReturnListItemDto {
  id: string;
  returnNumber: string;
  salesInvoiceId: string;
  customerId: string;
  status: string;
  lineCount: number;
  grandTotal: number;
  createdAt: string;
}

export interface SalesReturnListResponse {
  items: SalesReturnListItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

/** Línea de factura candidata a devolución, con el remanente ya calculado por el servidor. */
export interface ReturnableLineDto {
  invoiceDetailId: string;
  itemId: string | null;
  description: string;
  snapshotSku: string | null;
  warehouseId: string | null;
  uomCode: string;
  originalQuantity: number;
  returnedQuantity: number;
  remainingQuantity: number;
  unitPrice: number;
  discountPct: number;
  vatCode: string;
  vatRate: number;
  iceCode: string | null;
  iceRate: number;
}

// ── Payloads ────────────────────────────────────────────────────────────

export interface SalesReturnLineInput {
  invoiceDetailId: string;
  quantity: number;
}

export interface CreateSalesReturnDraftPayload {
  salesInvoiceId: string;
  reason: string;
  lines: SalesReturnLineInput[];
}

export interface UpdateSalesReturnDraftPayload {
  id: string;
  lines: SalesReturnLineInput[];
}

export interface AuthorizeSalesReturnRefundAllocationInput {
  method: SalesReturnRefundMethod;
  amount: number;
}

export interface AuthorizeSalesReturnPayload {
  refundAllocations: AuthorizeSalesReturnRefundAllocationInput[];
}

// ── Service ─────────────────────────────────────────────────────────────

/**
 * Consume exclusivamente los endpoints ya implementados de
 * `SalesReturnController` (ERP.API) — sin lógica de negocio propia, mismo
 * patrón que `salesService.ts` (envelope `apiGet/apiPost/apiPut/apiDelete`,
 * nunca axios directo).
 */
export const salesReturnService = {
  list: (search?: string, status?: string, page = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (search?.trim()) params.set("search", search.trim());
    if (status?.trim()) params.set("status", status.trim());
    params.set("pageNumber", String(page));
    params.set("pageSize", String(pageSize));
    return apiGet<SalesReturnListResponse>(`${BASE}?${params}`);
  },

  getById: (id: string) => apiGet<SalesReturnDto>(`${BASE}/${id}`),

  getReturnableLines: (invoiceId: string) =>
    apiGet<ReturnableLineDto[]>(
      `/api/v1/sales/invoices/${invoiceId}/returnable-lines`,
    ),

  createDraft: (payload: CreateSalesReturnDraftPayload) =>
    apiPost<SalesReturnDto>(BASE, payload),

  updateDraft: (id: string, payload: UpdateSalesReturnDraftPayload) =>
    apiPut<SalesReturnDto>(`${BASE}/${id}`, payload),

  cancelDraft: (id: string) => apiDelete<SalesReturnDto>(`${BASE}/${id}`),

  authorize: (id: string, payload: AuthorizeSalesReturnPayload) =>
    apiPost<SalesReturnDto>(`${BASE}/${id}/authorize`, payload),
};
