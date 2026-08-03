import { apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";

const BASE = "/api/v1/purchases/returns";

// ── DTOs (mismo contrato que PurchaseReturnDto/ReturnableLineDto en ERP.API) ──

export interface PurchaseReturnDetailDto {
  id: string;
  originalInvoiceDetailId: string;
  itemId: string;
  quantity: number;
  warehouseId: string;
}

export interface PurchaseReturnDto {
  id: string;
  purchaseInvoiceId: string;
  supplierId: string;
  branchId: string;
  returnNumber: string | null;
  reason: string;
  status: string;
  fiscalStatus: string;
  supplierCreditNoteDocumentId: string | null;
  authorizedSubtotal: number | null;
  authorizedVatTotal: number | null;
  authorizedIceTotal: number | null;
  authorizedDiscountTotal: number | null;
  authorizedGrandTotal: number | null;
  authorizedAtUtc: string | null;
  cancelledAtUtc: string | null;
  cancellationReason: string | null;
  lines: PurchaseReturnDetailDto[];
  createdAt: string;
  updatedAt: string | null;
}

export interface PurchaseReturnListResultDto {
  items: PurchaseReturnDto[];
  total: number;
  page: number;
  pageSize: number;
}

/** Línea de factura de compra candidata a devolución, con el remanente ya calculado por el servidor. */
export interface ReturnableLineDto {
  invoiceDetailId: string;
  itemId: string;
  description: string;
  originalQuantity: number;
  returnedQuantity: number;
  remainingQuantity: number;
  warehouseId: string;
}

export interface SupplierCreditNoteLinkDto {
  purchaseReturnId: string;
  fiscalStatus: string;
  supplierCreditNoteDocumentId: string;
  accessKey: string;
  invoiceNumber: string;
  totalAmount: number;
  currencyCode: string;
}

// ── Payloads ────────────────────────────────────────────────────────────

export interface PurchaseReturnLineInput {
  originalInvoiceDetailId: string;
  quantity: number;
}

export interface CreatePurchaseReturnDraftPayload {
  clientRequestId: string;
  purchaseInvoiceId: string;
  reason: string;
  lines: PurchaseReturnLineInput[];
}

export interface UpdatePurchaseReturnDraftPayload {
  reason: string;
  lines: PurchaseReturnLineInput[];
}

export interface CancelPurchaseReturnPayload {
  reason: string;
  clientRequestId: string;
}

export interface LinkSupplierCreditNotePayload {
  accessKey: string;
  supplierRuc: string;
  supplierName: string;
  invoiceNumber: string;
  issueDate: string;
  subtotal: number;
  vatAmount: number;
  totalAmount: number;
  currencyCode: string;
  clientRequestId: string;
}

// ── Service ─────────────────────────────────────────────────────────────

/**
 * Consume exclusivamente los endpoints ya implementados de
 * `PurchaseReturnController` (ERP.API, Fase 11) — sin lógica de negocio
 * propia, mismo patrón que `salesReturnService.ts` (envelope
 * `apiGet/apiPost/apiPut`, nunca axios directo). El backend no admite filtro
 * por texto libre (`GetPurchaseReturnListQuery` solo acepta `status`) — a
 * diferencia de `salesReturnService.list`, no expone parámetro `search`.
 */
export const purchaseReturnService = {
  list: (status?: string, page = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (status?.trim()) params.set("status", status.trim());
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    return apiGet<PurchaseReturnListResultDto>(`${BASE}?${params}`);
  },

  getById: (id: string) => apiGet<PurchaseReturnDto>(`${BASE}/${id}`),

  getReturnableLines: (invoiceId: string) =>
    apiGet<ReturnableLineDto[]>(
      `/api/v1/purchases/invoices/${invoiceId}/returnable-lines`,
    ),

  createDraft: (payload: CreatePurchaseReturnDraftPayload) =>
    apiPost<PurchaseReturnDto>(BASE, payload),

  updateDraft: (id: string, payload: UpdatePurchaseReturnDraftPayload) =>
    apiPut<PurchaseReturnDto>(`${BASE}/${id}`, payload),

  authorize: (id: string, clientRequestId: string) =>
    apiPost<PurchaseReturnDto>(`${BASE}/${id}/authorize`, { clientRequestId }),

  cancel: (id: string, payload: CancelPurchaseReturnPayload) =>
    apiPost<PurchaseReturnDto>(`${BASE}/${id}/cancel`, payload),

  linkCreditNote: (id: string, payload: LinkSupplierCreditNotePayload) =>
    apiPost<SupplierCreditNoteLinkDto>(`${BASE}/${id}/credit-note`, payload),
};
