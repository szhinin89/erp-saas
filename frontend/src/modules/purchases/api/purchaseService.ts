import { apiGet, apiPost, apiPut } from '../../lib/apiEnvelope';

const BASE = '/api/v1/purchases';

// ── DTOs ─────────────────────────────────────────────────────────────────

export interface PurchaseLineDto {
  id: string;
  itemId: string | null;
  description: string;
  // ── Product Snapshot ────────────────────────────────────────────
  snapshotSku: string | null;
  snapshotItemName: string | null;
  snapshotSupplierCode: string | null;
  // ── UoM ─────────────────────────────────────────────────────────
  uomCode: string;
  conversionFactor: number;
  quantityInBaseUom: number;
  // ── Quantity & Price ────────────────────────────────────────────
  quantity: number;
  unitPrice: number;
  discountPct: number;
  discountAmount: number;
  // ── Costs ───────────────────────────────────────────────────────
  freightAllocated: number;
  otherCostsAllocated: number;
  totalLineCost: number;
  landedUnitCost: number;
  taxableBase: number;
  // ── VAT ─────────────────────────────────────────────────────────
  vatCode: string;
  vatRate: number;
  vatAmount: number;
  snapshotVatName: string | null;
  // ── ICE ─────────────────────────────────────────────────────────
  iceCode: string | null;
  iceRate: number;
  iceAmount: number;
  snapshotIceName: string | null;
  // ── Total ───────────────────────────────────────────────────────
  taxInclusiveTotal: number;
  // ── Analytic ────────────────────────────────────────────────────
  snapshotItemPvp: number;
  // ── Warehouse ───────────────────────────────────────────────────
  warehouseId: string | null;
  snapshotWarehouseCode: string | null;
  // ── PO Traceability ─────────────────────────────────────────────
  purchaseOrderDetailId: string | null;
  orderedQuantity: number | null;
  // ── Meta ────────────────────────────────────────────────────────
  notes: string | null;
  sortOrder: number;
}

export interface PurchasePaymentScheduleDto {
  id: string;
  installmentNumber: number;
  dueDate: string;
  amount: number;
  notes: string | null;
}

export interface PurchaseInvoiceDto {
  id: string;
  supplierId: string;
  supplierName: string;
  supplierTaxId: string;
  docTypeCode: string;
  invoiceNumber: string;
  issueDate: string;
  accessKey: string | null;
  authorizationNumber: string | null;
  authorizationDate: string | null;
  taxSupportCode: string | null;
  sriPaymentMethodCode: string | null;
  sriPaymentMethodName: string | null;
  currencyCode: string;
  exchangeRate: number;
  purchaseOrderId: string | null;
  purchaseOrderNumber: string | null;
  globalWarehouseId: string | null;
  paymentTermId: string;
  paymentTermName: string;
  paymentTermInstallments: number;
  paymentTermDaysBetween: number;
  creditTermDays: number;
  dueDate: string | null;
  notes: string | null;
  status: string;
  cancelReason: string | null;
  cancelledAt: string | null;
  cancelledBy: string | null;
  subtotal: number;
  totalDiscount: number;
  totalIce: number;
  totalVat: number;
  totalFreight: number;
  totalOtherCosts: number;
  grandTotal: number;
  lines: PurchaseLineDto[];
  paymentSchedules: PurchasePaymentScheduleDto[];
  createdAt: string;
  updatedAt: string | null;
}

export interface PurchaseListItemDto {
  id: string;
  invoiceNumber: string;
  issueDate: string;
  supplierId: string;
  status: string;
  lineCount: number;
  createdAt: string;
}

export interface PurchaseListResponse {
  items: PurchaseListItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

// ── Payloads ─────────────────────────────────────────────────────────────

export interface PurchaseLineInput {
  itemId?: string | null;
  description: string;
  quantity: number;
  unitPrice: number;
  vatCode: string;
  discountPct?: number;
  iceCode?: string | null;
  warehouseId?: string | null;
  notes?: string | null;
  purchaseOrderDetailId?: string | null;
  orderedQuantity?: number | null;
}

export interface CreatePurchasePayload {
  supplierId: string;
  docTypeCode: string;
  invoiceNumber: string;
  issueDate: string;
  lines: PurchaseLineInput[];
  accessKey?: string | null;
  authorizationNumber?: string | null;
  authorizationDate?: string | null;
  taxSupportCode?: string | null;
  sriPaymentMethodCode?: string | null;
  globalWarehouseId?: string | null;
  freightCost?: number;
  otherCosts?: number;
  dueDate?: string | null;
  notes?: string | null;
  paymentTermId?: string | null;
}

export interface UpdatePurchasePayload extends CreatePurchasePayload {
  id: string;
}

export interface ConfirmScheduleInput {
  installmentNumber: number;
  dueDate: string;
  amount: number;
  notes?: string | null;
}

// ── Service ──────────────────────────────────────────────────────────────

export interface ApplyDiscountPayload { discountPct: number; }

export interface PurchaseItemContextDto {
  itemId: string;
  sku: string;
  shortName: string;
  description: string;
  supplierCode: string | null;
  currentStock: number;
  availableStock: number;
  reservedStock: number;
  averageCost: number;
  lastPurchaseCost: number;
  pvp: number;
  previousPrice: number;
  maxDiscountPercent: number;
  purchaseVatCode: string | null;
  vatPercent: number;
  exciseTaxCode: string | null;
  icePercent: number;
  hasVat: boolean;
  hasIce: boolean;
  costMargin: number;
  costMarginPercent: number;
}

export const purchaseService = {
  list: (search?: string, status?: string, page = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (search?.trim()) params.set('search', search.trim());
    if (status?.trim()) params.set('status', status.trim());
    params.set('pageNumber', String(page));
    params.set('pageSize', String(pageSize));
    return apiGet<PurchaseListResponse>(`${BASE}?${params}`);
  },
  getById: (id: string) => apiGet<PurchaseInvoiceDto>(`${BASE}/${id}`),
  create: (p: CreatePurchasePayload) => apiPost<PurchaseInvoiceDto>(BASE, p),
  update: (id: string, p: UpdatePurchasePayload) => apiPut<PurchaseInvoiceDto>(`${BASE}/${id}`, p),
  applyDiscount: (id: string, discountPct: number) => apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/apply-discount`, { discountPct }),
  allocateFreight: (id: string) => apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/allocate-freight`, {}),
  recalculate: (id: string) => apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/recalculate`, {}),
  confirm: (id: string, schedule?: ConfirmScheduleInput[]) =>
    apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/confirm`, { schedule: schedule ?? null }),
  cancel: (id: string, reason: string) => apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/cancel`, { reason }),

  retentionPreview: (id: string) => apiGet<RetentionPreviewDto>(`${BASE}/${id}/retention-preview`),
  getWithholding: (id: string) => apiGet<IssuedWithholdingDto | null>(`${BASE}/${id}/withholding`),
  issueWithholding: (id: string, emissionPointId: string, issueDate: string) =>
    apiPost<IssuedWithholdingDto>(`${BASE}/${id}/withholding`, { emissionPointId, issueDate }),
  getWithholdingById: (whId: string) => apiGet<IssuedWithholdingDto>(`${BASE}/withholdings/${whId}`),
  cancelWithholding: (whId: string, reason: string) =>
    apiPost<IssuedWithholdingDto>(`${BASE}/withholdings/${whId}/cancel`, { reason }),

  // supplierId opcional: si se indica, el backend resuelve el código de proveedor
  // específico (ItemSupplierCode), única fuente de códigos de compra.
  getItemContext: (itemId: string, warehouseId: string, supplierId?: string) =>
    apiGet<PurchaseItemContextDto>(
      `${BASE}/items/context?itemId=${itemId}&warehouseId=${warehouseId}${supplierId ? `&supplierId=${supplierId}` : ''}`
    ),
};

// ── Withholding DTOs ─────────────────────────────────────────────────────

export interface RetentionLinePreviewDto {
  taxType: string;
  retentionCode: string;
  retentionCodeName: string;
  taxableBase: number;
  retentionPct: number;
  amountRetained: number;
}

export interface RetentionPreviewDto {
  lines: RetentionLinePreviewDto[];
  totalRetainedVat: number;
  totalRetainedIncome: number;
  totalRetainedIsd: number;
  totalRetained: number;
  skipReason: string | null;
}

export interface IssuedWithholdingDetailDto {
  id: string;
  taxType: string;
  retentionCode: string;
  retentionCodeDescription: string;
  taxableBase: number;
  retentionPct: number;
  amountRetained: number;
}

export interface IssuedWithholdingDto {
  id: string;
  purchaseInvoiceId: string;
  supplierId: string;
  emissionPointId: string;
  withholdingNumber: string;
  issueDate: string;
  accessKey: string | null;
  totalRetainedVat: number;
  totalRetainedIncome: number;
  totalRetainedIsd: number;
  totalRetained: number;
  status: string;
  details: IssuedWithholdingDetailDto[];
  createdAt: string;
  updatedAt: string | null;
}
