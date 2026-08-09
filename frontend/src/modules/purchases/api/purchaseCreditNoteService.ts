import { apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";

const BASE = "/api/v1/purchases/credit-notes";

// ── DTOs (mismo contrato que PurchaseCreditNoteDto/PurchaseCreditNoteDetailDto en ERP.API,
// FLOW-READY-02C.2) ──

export interface PurchaseCreditNoteDetailDto {
  id: string;
  description: string;
  subtotal: number;
  vatCode: string | null;
  vatRate: number | null;
  vatAmount: number;
  totalAmount: number;
}

/** FLOW-READY-02C-R1.1 — cómo se aplica la nota de crédito contra la factura afectada. */
export type PurchaseCreditNoteApplicationType = "Return" | "Discount";

/**
 * FLOW-READY-02C-R1.2 — línea de descuento por resumen fiscal de compra, flujo principal de
 * `ApplicationType.Discount`. VatCode/VatRate/nombres/IceCode/IceRate son siempre heredados del
 * `PurchaseInvoiceTaxSummary` real de la compra — nunca editables aquí.
 */
export interface PurchaseCreditNoteTaxSummaryDto {
  id: string;
  sourcePurchaseInvoiceTaxSummaryId: string;
  vatCode: string;
  vatRate: number;
  vatName: string | null;
  iceCode: string | null;
  iceRate: number;
  iceName: string | null;
  taxableBase: number;
  iceAmount: number;
  vatAmount: number;
  totalAmount: number;
}

export interface PurchaseCreditNoteDto {
  id: string;
  purchaseInvoiceId: string;
  supplierId: string;
  branchId: string;
  receptionDocumentId: string | null;
  applicationType: PurchaseCreditNoteApplicationType;
  linkedPurchaseReturnId: string | null;
  status: string;
  creditNoteNumber: string;
  accessKey: string | null;
  authorizationNumber: string | null;
  authorizationDate: string | null;
  issueDate: string;
  reason: string;
  subtotal: number;
  iceAmount: number;
  vatAmount: number;
  totalAmount: number;
  appliedToPayableAmount: number | null;
  authorizedAtUtc: string | null;
  cancelledAtUtc: string | null;
  cancellationReason: string | null;
  lines: PurchaseCreditNoteDetailDto[];
  taxSummaries: PurchaseCreditNoteTaxSummaryDto[];
  createdAt: string;
  updatedAt: string | null;
  /** Enriquecimiento — solo poblado cuando el handler ya cargó la factura (prácticamente siempre). */
  invoiceNumber: string | null;
  supplierName: string | null;
  invoiceBalanceDue: number | null;
  receptionDocumentAccessKey: string | null;
}

export interface PurchaseCreditNoteListItemDto {
  id: string;
  purchaseInvoiceId: string;
  supplierId: string;
  applicationType: PurchaseCreditNoteApplicationType;
  status: string;
  creditNoteNumber: string;
  totalAmount: number;
  issueDate: string;
  authorizedAtUtc: string | null;
  createdAt: string;
}

export interface PurchaseCreditNoteListResultDto {
  items: PurchaseCreditNoteListItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

// ── Payloads ────────────────────────────────────────────────────────────

export interface PurchaseCreditNoteDraftLineInput {
  description: string;
  subtotal: number;
  vatCode: string | null;
  vatRate: number | null;
  vatAmount: number;
}

/** FLOW-READY-02C-R1.2 — solo referencia + base a descontar; el backend resuelve el resto. */
export interface PurchaseCreditNoteTaxSummaryLineInput {
  sourcePurchaseInvoiceTaxSummaryId: string;
  taxableBase: number;
}

export interface CreatePurchaseCreditNoteDraftPayload {
  clientRequestId: string;
  purchaseInvoiceId: string;
  receptionDocumentId: string | null;
  applicationType: PurchaseCreditNoteApplicationType;
  creditNoteNumber: string;
  accessKey: string | null;
  authorizationNumber: string | null;
  authorizationDate: string | null;
  issueDate: string;
  reason: string;
  lines: PurchaseCreditNoteDraftLineInput[];
  taxSummaryLines?: PurchaseCreditNoteTaxSummaryLineInput[];
}

export interface UpdatePurchaseCreditNoteDraftPayload {
  creditNoteNumber: string;
  accessKey: string | null;
  authorizationNumber: string | null;
  authorizationDate: string | null;
  issueDate: string;
  reason: string;
  lines: PurchaseCreditNoteDraftLineInput[];
  taxSummaryLines?: PurchaseCreditNoteTaxSummaryLineInput[];
}

export interface CancelPurchaseCreditNotePayload {
  reason: string;
  clientRequestId: string;
}

/** FLOW-READY-02C-R1.1 — cuerpo de POST /{id}/link-return. */
export interface LinkPurchaseCreditNoteToReturnPayload {
  purchaseReturnId: string;
  clientRequestId: string;
}

export interface PurchaseCreditNoteListFilters {
  status?: string;
  supplierId?: string;
  invoiceId?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}

// ── Service ─────────────────────────────────────────────────────────────

/**
 * Consume exclusivamente los endpoints ya implementados de
 * `PurchaseCreditNoteController` (ERP.API, FLOW-READY-02C.2) — sin lógica de
 * negocio propia, mismo patrón que `purchaseReturnService.ts` (envelope
 * `apiGet/apiPost/apiPut`, nunca axios directo).
 */
export const purchaseCreditNoteService = {
  list: (filters: PurchaseCreditNoteListFilters = {}) => {
    const params = new URLSearchParams();
    if (filters.status?.trim()) params.set("status", filters.status.trim());
    if (filters.supplierId) params.set("supplierId", filters.supplierId);
    if (filters.invoiceId) params.set("invoiceId", filters.invoiceId);
    if (filters.dateFrom) params.set("dateFrom", filters.dateFrom);
    if (filters.dateTo) params.set("dateTo", filters.dateTo);
    params.set("page", String(filters.page ?? 1));
    params.set("pageSize", String(filters.pageSize ?? 25));
    return apiGet<PurchaseCreditNoteListResultDto>(`${BASE}?${params}`);
  },

  getById: (id: string) => apiGet<PurchaseCreditNoteDto>(`${BASE}/${id}`),

  createDraft: (payload: CreatePurchaseCreditNoteDraftPayload) =>
    apiPost<PurchaseCreditNoteDto>(BASE, payload),

  updateDraft: (id: string, payload: UpdatePurchaseCreditNoteDraftPayload) =>
    apiPut<PurchaseCreditNoteDto>(`${BASE}/${id}`, payload),

  authorize: (id: string, clientRequestId: string) =>
    apiPost<PurchaseCreditNoteDto>(`${BASE}/${id}/authorize`, { clientRequestId }),

  cancel: (id: string, payload: CancelPurchaseCreditNotePayload) =>
    apiPost<PurchaseCreditNoteDto>(`${BASE}/${id}/cancel`, payload),

  /** FLOW-READY-02C-R1.1 — vincula una nota de crédito tipo Devolución al PurchaseReturn que la aplica. */
  linkReturn: (id: string, payload: LinkPurchaseCreditNoteToReturnPayload) =>
    apiPost<PurchaseCreditNoteDto>(`${BASE}/${id}/link-return`, payload),
};
