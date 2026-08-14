import { apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";

const BASE = "/api/v1/purchases";

// ── DTOs ─────────────────────────────────────────────────────────────────

/**
 * FLOW-READY-02D.1/02C-R1.2 — resumen fiscal persistido de una compra confirmada, agrupado por
 * combinación de impuesto. `creditedTaxableBase`/`availableTaxableBase` reflejan cuánta base ya fue
 * acreditada por notas de crédito de compra tipo Descuento no canceladas.
 */
export interface PurchaseInvoiceTaxSummaryDto {
  id: string;
  vatCode: string;
  vatRate: number;
  vatName: string | null;
  iceCode: string | null;
  iceRate: number;
  iceName: string | null;
  // FLOW-READY-02F.1 — dimensión IRBPNR, aditiva; siempre null/0 en facturas sin ese impuesto.
  irbpnrCode: string | null;
  irbpnrRate: number;
  irbpnrName: string | null;
  taxableBase: number;
  iceAmount: number;
  vatAmount: number;
  irbpnrAmount: number;
  totalAmount: number;
  creditedTaxableBase: number;
  availableTaxableBase: number;
}

/** FLOW-READY-02F.1 — snapshot fiel de un impuesto del XML (IVA/ICE/IRBPNR), solo lectura. */
export interface PurchaseInvoiceDetailTaxDto {
  taxCode: string;
  taxRateCode: string;
  taxName: string;
  rate: number | null;
  calculationType: string;
  taxableBase: number;
  taxAmount: number;
  source: string;
}

export interface PurchaseLineDto {
  id: string;
  itemId: string | null;
  description: string;
  // ── Product Snapshot ────────────────────────────────────────────
  snapshotSku: string | null;
  snapshotItemName: string | null;
  snapshotSupplierCode: string | null;
  // ── UoM ─────────────────────────────────────────────────────────
  packagingLevelId: string | null;
  uomCode: string;
  baseUomCode: string;
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
  // ── IRBPNR (FLOW-READY-02F.1 — sin campos escalares legacy: derivado de Taxes) ───
  irbpnrCode: string | null;
  irbpnrRate: number | null;
  irbpnrAmount: number;
  snapshotIrbpnrName: string | null;
  // ── Detalle fiel de impuestos del XML (IVA/ICE/IRBPNR) ───────────────────────
  taxes: PurchaseInvoiceDetailTaxDto[];
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
  // ── Purchase Reception Traceability ──────────────────────────────
  purchaseReceptionLineId: string | null;
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
  // FLOW-READY-02F.1 — informativo, NUNCA incluido en grandTotal (soporte contable pendiente).
  totalIrbpnr: number;
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

export interface PurchaseAccessKeyLookupDto {
  exists: boolean;
  purchaseId: string | null;
  invoiceNumber: string | null;
  supplierName: string | null;
  accessKey: string | null;
}

export interface PurchasesReportRowDto {
  id: string;
  issueDate: string;
  supplierId: string;
  supplierName: string;
  supplierTaxId: string;
  invoiceNumber: string;
  status: string;
  subtotal: number;
  totalVat: number;
  totalDiscount: number;
  grandTotal: number;
}

export interface PurchasesReportTotalsDto {
  count: number;
  subtotal: number;
  totalVat: number;
  totalDiscount: number;
  grandTotal: number;
}

export interface PurchasesBySupplierReportResponse {
  items: PurchasesReportRowDto[];
  totals: PurchasesReportTotalsDto;
  dateFrom: string;
  dateTo: string;
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
  purchaseReceptionLineId?: string | null;
  packagingLevelId?: string | null;
  // PURCHASE-DISTRIBUTE-COST-BEFORE-SAVE-01 — prorrateo ya revisado en el modal "Distribuir
  // flete/gasto" antes de guardar; el backend lo aplica directo por línea sin reprorratear.
  freightAllocated?: number | null;
  otherCostsAllocated?: number | null;
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

export interface ApplyDiscountPayload {
  discountPct: number;
}

/** PURCHASE-FREIGHT-DISTRIBUTION-MODAL-01 — debe coincidir con PurchaseCostType (backend). */
export type PurchaseCostDistributionType = "Freight" | "OtherCost";

export interface PurchaseItemContextDto {
  itemId: string;
  sku: string;
  shortName: string;
  description: string;
  baseUomCode: string;
  tracksStock: boolean;
  packagingLevels: PurchaseItemPackagingLevelDto[];
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

export interface PurchaseItemPackagingLevelDto {
  id: string;
  name: string;
  baseQuantity: number;
  uomCode: string;
  isBaseUnit: boolean;
  isPurchaseDefault: boolean;
}

export const purchaseService = {
  list: (search?: string, status?: string, page = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (search?.trim()) params.set("search", search.trim());
    if (status?.trim()) params.set("status", status.trim());
    params.set("pageNumber", String(page));
    params.set("pageSize", String(pageSize));
    return apiGet<PurchaseListResponse>(`${BASE}?${params}`);
  },
  getById: (id: string) => apiGet<PurchaseInvoiceDto>(`${BASE}/${id}`),
  getByAccessKey: (accessKey: string) =>
    apiGet<PurchaseAccessKeyLookupDto>(
      `${BASE}/by-access-key/${encodeURIComponent(accessKey.trim())}`,
    ),
  create: (p: CreatePurchasePayload) => apiPost<PurchaseInvoiceDto>(BASE, p),
  update: (id: string, p: UpdatePurchasePayload) =>
    apiPut<PurchaseInvoiceDto>(`${BASE}/${id}`, p),
  applyDiscount: (id: string, discountPct: number) =>
    apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/apply-discount`, {
      discountPct,
    }),
  allocateFreight: (id: string) =>
    apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/allocate-freight`, {}),
  recalculate: (id: string) =>
    apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/recalculate`, {}),
  // PURCHASE-FREIGHT-DISTRIBUTION-MODAL-01 — suma `amount` (flete/otro gasto aún no registrado)
  // solo entre `includedLineIds`, sin tocar el resto de líneas. Distinto de allocateFreight (que
  // reprorratea el total ya persistido entre TODAS las líneas).
  distributeCost: (
    id: string,
    costType: PurchaseCostDistributionType,
    amount: number,
    includedLineIds: string[],
  ) =>
    apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/distribute-cost`, {
      costType,
      amount,
      includedLineIds,
    }),
  confirm: (id: string, schedule?: ConfirmScheduleInput[]) =>
    apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/confirm`, {
      schedule: schedule ?? null,
    }),
  cancel: (id: string, reason: string) =>
    apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/cancel`, { reason }),

  retentionPreview: (id: string) =>
    apiGet<RetentionPreviewDto>(`${BASE}/${id}/retention-preview`),
  getWithholding: (id: string) =>
    apiGet<IssuedWithholdingDto | null>(`${BASE}/${id}/withholding`),
  issueWithholding: (id: string, emissionPointId: string, issueDate: string) =>
    apiPost<IssuedWithholdingDto>(`${BASE}/${id}/withholding`, {
      emissionPointId,
      issueDate,
    }),
  getWithholdingById: (whId: string) =>
    apiGet<IssuedWithholdingDto>(`${BASE}/withholdings/${whId}`),
  cancelWithholding: (whId: string, reason: string) =>
    apiPost<IssuedWithholdingDto>(`${BASE}/withholdings/${whId}/cancel`, {
      reason,
    }),

  // supplierId opcional: si se indica, el backend resuelve el código de proveedor
  // específico (ItemSupplierCode), única fuente de códigos de compra.
  getItemContext: (itemId: string, warehouseId: string, supplierId?: string) =>
    apiGet<PurchaseItemContextDto>(
      `${BASE}/items/context?itemId=${itemId}&warehouseId=${warehouseId}${supplierId ? `&supplierId=${supplierId}` : ""}`,
    ),

  /** Reporte básico de compras por proveedor. Sin fechas, el backend usa el día actual. */
  supplierReport: (dateFrom?: string, dateTo?: string, supplierId?: string) => {
    const params = new URLSearchParams();
    if (dateFrom) params.set("dateFrom", dateFrom);
    if (dateTo) params.set("dateTo", dateTo);
    if (supplierId) params.set("supplierId", supplierId);
    const qs = params.toString();
    const url = qs ? `${BASE}/report?${qs}` : `${BASE}/report`;
    return apiGet<PurchasesBySupplierReportResponse>(url);
  },

  /** FLOW-READY-02D.1 — resumen fiscal persistido de la compra, agrupado por impuesto. Vacío si la compra aún no fue confirmada o no tiene resumen persistido (nunca inventado en frontend). */
  getTaxSummaries: (invoiceId: string) =>
    apiGet<PurchaseInvoiceTaxSummaryDto[]>(`${BASE}/invoices/${invoiceId}/tax-summaries`),
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
