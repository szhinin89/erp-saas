import { apiDelete, apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";
import type { RetentionIntentRequest } from "../../retentions/facades/purchaseRetentionFacade";
import type { PurchaseDraftLineDto } from "./purchaseReceptionService";

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
  purchaseReceptionLineTaxId?: string | null;
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
  xmlAllocatedSubtotal?: number | null;
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
  /**
   * PURCHASE-DRAFT-RECOVERY F1 — evidencia original de la línea de recepción (mismo contrato que
   * "Crear compra" desde Recepción). Solo al leer un borrador por Id; null en líneas manuales.
   */
  xmlSource?: PurchaseDraftLineDto | null;
  /**
   * FASE 5 PURCHASE-UX — presentación de origen XML persistida (interpretación del documento del
   * proveedor), separada de la operativa. null en líneas manuales y borradores anteriores.
   */
  xmlOriginPackagingLevelId?: string | null;
  xmlOriginFactor?: number | null;
}

/** PURCHASE-DRAFT-RECOVERY F3 — decisión de retención recordada por el borrador. */
export interface PurchaseRetentionIntentDraftDto {
  appliesRetention: boolean;
  emissionPointId: string | null;
  issueDate: string | null;
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
  // FLOW-READY-02F.2 — incluido en grandTotal (TaxInclusiveTotal de cada línea, backend).
  totalIrbpnr: number;
  lines: PurchaseLineDto[];
  paymentSchedules: PurchasePaymentScheduleDto[];
  createdAt: string;
  updatedAt: string | null;
  /** PURCHASE-DRAFT-RECOVERY F3 — null si el borrador nunca registró una decisión. */
  retentionIntent?: PurchaseRetentionIntentDraftDto | null;
}

export interface PurchaseListItemDto {
  id: string;
  invoiceNumber: string;
  issueDate: string;
  supplierId: string;
  status: string;
  lineCount: number;
  createdAt: string;
  // FASE 5 PURCHASE-UX — snapshot del proveedor y total del documento para el listado.
  supplierName: string;
  supplierTaxId: string;
  grandTotal: number;
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

export interface PurchaseXmlAllocationInput {
  subtotal: number;
  discount: number;
  taxes: { purchaseReceptionLineTaxId: string; taxableBase: number; taxAmount: number }[];
}

export interface PurchaseLineInput {
  xmlAllocation?: PurchaseXmlAllocationInput;
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
  // FASE 5 PURCHASE-UX — presentación de origen XML (solo líneas de recepción); el backend resuelve
  // el factor desde el nivel del ítem. No aprende nada del proveedor.
  xmlOriginPackagingLevelId?: string | null;
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
  /**
   * PURCHASE-DRAFT-RECOVERY F2 — cuotas manuales del borrador: [] = descartarlas (se generan desde
   * la condición de pago al confirmar); con filas = reemplazarlas (deben cuadrar con el total).
   */
  schedule?: ConfirmScheduleInput[];
  /** PURCHASE-DRAFT-RECOVERY F3 — intención de retención recordada por el borrador. */
  retentionIntent?: PurchaseRetentionIntentDraftDto;
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
  participatesInInventory: boolean;
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
  list: (search?: string, status?: string, page = 1, pageSize = 25, supplierId?: string) => {
    const params = new URLSearchParams();
    if (search?.trim()) params.set("search", search.trim());
    if (status?.trim()) params.set("status", status.trim());
    params.set("pageNumber", String(page));
    params.set("pageSize", String(pageSize));
    if (supplierId) params.set("supplierId", supplierId);
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
  /**
   * ZH-PURCHASE-RETENTION-CONFIRM-01 — `retention` es la intención opcional de emitir la retención
   * dentro de esta misma confirmación (compra + CxP neta + retención + asientos, todo o nada).
   * Sin intención, confirma igual que siempre.
   */
  confirm: (id: string, schedule?: ConfirmScheduleInput[], retention?: RetentionIntentRequest) =>
    apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/confirm`, {
      schedule: schedule ?? null,
      retention: retention ?? null,
    }),
  /**
   * Anula la compra. `requestSriAnnulment` (ZH-RETENTION-SRI-ANNULMENT-01): si su retención ya está
   * AUTORIZADA por el SRI, inicia la anulación ante el SRI en lugar de anular — la compra sigue
   * CONFIRMADA (el DTO devuelto lo refleja) hasta que el SRI confirme ANULADO.
   */
  cancel: (id: string, reason: string, requestSriAnnulment = false) =>
    apiPost<PurchaseInvoiceDto>(`${BASE}/${id}/cancel`, { reason, requestSriAnnulment }),

  /**
   * FASE 5 PURCHASE-UX — descarta un BORRADOR (soft: queda Discarded, se conserva el historial). Sin
   * reversos; la recepción/XML vuelve a quedar disponible. Una compra confirmada usa `cancel`.
   */
  discardDraft: (id: string, reason?: string) =>
    apiDelete<PurchaseInvoiceDto>(`${BASE}/${id}`, { params: { reason: reason?.trim() || undefined } }),

  /** Vista previa de la retención de una compra en BORRADOR (elegibilidad + montos propuestos). */
  retentionPreview: (id: string) =>
    apiGet<RetentionPreviewDto>(`${BASE}/${id}/retention-preview`),

  // supplierId opcional: si se indica, el backend resuelve el código de proveedor
  // específico (ItemSupplierCode), única fuente de códigos de compra.
  // warehouseId opcional (PURCHASES-HARDENING-P0 B8): sin bodega no hay stock/último costo, pero
  // sí `participatesInInventory`.
  getItemContext: (itemId: string, warehouseId?: string | null, supplierId?: string) =>
    apiGet<PurchaseItemContextDto>(
      `${BASE}/items/context?itemId=${itemId}${warehouseId ? `&warehouseId=${warehouseId}` : ""}${supplierId ? `&supplierId=${supplierId}` : ""}`,
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
