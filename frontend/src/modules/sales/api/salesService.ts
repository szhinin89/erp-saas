import { apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";

const BASE = "/api/v1/sales";
const PM_BASE = "/api/v1/payment-methods";

// ── DTOs ─────────────────────────────────────────────────────────────────

export interface SalesInvoiceDetailDto {
  id: string;
  itemId: string | null;
  warehouseId: string | null;
  description: string;
  snapshotSku: string | null;
  snapshotItemName: string | null;
  uomCode: string;
  conversionFactor: number;
  quantityInBaseUom: number;
  quantity: number;
  unitPrice: number;
  discountPct: number;
  discountAmount: number;
  taxableBase: number;
  vatCode: string;
  vatRate: number;
  vatAmount: number;
  snapshotVatName: string | null;
  iceCode: string | null;
  iceRate: number;
  iceAmount: number;
  snapshotIceName: string | null;
  taxInclusiveTotal: number;
  notes: string | null;
  sortOrder: number;
}

export interface CardDetailDto {
  cardBrand: string | null;
  cardLastFour: string | null;
  bankName: string | null;
  authorizationCode: string | null;
  lotNumber: string | null;
}
export interface TransferDetailDto {
  bankName: string | null;
  receiptNumber: string | null;
  transferDate: string | null;
}
export interface ChequeDetailDto {
  bankName: string | null;
  chequeNumber: string | null;
  holderName: string | null;
  cashDate: string | null;
}

export interface SalesInvoicePaymentDto {
  id: string;
  paymentMethodId: string;
  paymentMethodCode: string;
  paymentMethodName: string;
  amount: number;
  reference: string | null;
  cardDetail: CardDetailDto | null;
  transferDetail: TransferDetailDto | null;
  chequeDetail: ChequeDetailDto | null;
}

export interface CardDetailInput {
  cardBrand?: string;
  cardLastFour?: string;
  bankName?: string;
  authorizationCode?: string;
  lotNumber?: string;
}
export interface TransferDetailInput {
  bankName?: string;
  receiptNumber?: string;
  transferDate?: string;
}
export interface ChequeDetailInput {
  bankName?: string;
  chequeNumber?: string;
  holderName?: string;
  cashDate?: string;
}

export interface SalesPaymentInput {
  paymentMethodId: string;
  amount: number;
  reference?: string | null;
  cardDetail?: CardDetailInput | null;
  transferDetail?: TransferDetailInput | null;
  chequeDetail?: ChequeDetailInput | null;
}

/** Esquema de detalle que la UI debe capturar al registrar un pago — viene del catálogo, nunca se infiere del código. */
export type PaymentMethodDetailType = "None" | "Card" | "Transfer" | "Check";

export interface PaymentMethodDto {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  requiresReference: boolean;
  isCreditAllowed: boolean;
  sortOrder: number;
  detailType: PaymentMethodDetailType;
}

export interface SalesInvoiceDto {
  id: string;
  customerId: string;
  customerName: string;
  customerTaxId: string;
  customerIdentificationType: string;
  customerEmail: string | null;
  customerAddress: string | null;
  docTypeCode: string;
  sriPaymentMethodCode: string | null;
  invoiceNumber: string;
  issueDate: string;
  cashSessionId: string;
  emissionPointId: string | null;
  emissionType: string;
  currencyCode: string;
  exchangeRate: number;
  paymentTermId: string;
  paymentTermName: string;
  paymentTermInstallments: number;
  paymentTermDaysBetween: number;
  creditTermDays: number;
  dueDate: string | null;
  notes: string | null;
  status: string;
  electronicStatus: string;
  accessKey: string | null;
  authorizationNumber: string | null;
  authorizationDate: string | null;
  subtotal: number;
  totalDiscount: number;
  totalIce: number;
  totalVat: number;
  totalTax: number;
  grandTotal: number;
  payments: SalesInvoicePaymentDto[];
  lines: SalesInvoiceDetailDto[];
  createdAt: string;
  updatedAt: string | null;
  /** Motivo del fallo si la emisión electrónica falló en el intento más reciente; null si no aplica o tuvo éxito. */
  electronicIssueError: string | null;
}

export interface SalesListItemDto {
  id: string;
  invoiceNumber: string;
  issueDate: string;
  customerId: string;
  customerName: string;
  status: string;
  lineCount: number;
  grandTotal: number;
  createdAt: string;
}

export interface SalesListResponse {
  items: SalesListItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface SalesReportRowDto {
  id: string;
  invoiceNumber: string;
  issueDate: string;
  customerId: string;
  customerName: string;
  subtotal: number;
  totalVat: number;
  totalDiscount: number;
  grandTotal: number;
  status: string;
  emissionType: string;
}

export interface SalesReportTotalsDto {
  count: number;
  subtotal: number;
  totalVat: number;
  totalDiscount: number;
  grandTotal: number;
}

export interface SalesReportResponse {
  items: SalesReportRowDto[];
  totals: SalesReportTotalsDto;
  dateFrom: string;
  dateTo: string;
}

// ── Payloads ─────────────────────────────────────────────────────────────

export interface SalesLineInput {
  itemId?: string | null;
  warehouseId?: string | null;
  description: string;
  quantity: number;
  unitPrice: number;
  vatCode: string;
  discountPct?: number;
  iceCode?: string | null;
  notes?: string | null;
}

export interface CreateSalesPayload {
  customerId: string;
  issueDate: string;
  lines: SalesLineInput[];
  dueDate?: string | null;
  notes?: string | null;
  paymentTermId?: string | null;
  payments?: SalesPaymentInput[];
  docTypeCode?: string | null;
  sriPaymentMethodCode?: string | null;
}

export interface UpdateSalesPayload extends CreateSalesPayload {
  id: string;
}

// ── Service ──────────────────────────────────────────────────────────────

export const salesService = {
  list: (search?: string, status?: string, page = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (search?.trim()) params.set("search", search.trim());
    if (status?.trim()) params.set("status", status.trim());
    params.set("pageNumber", String(page));
    params.set("pageSize", String(pageSize));
    return apiGet<SalesListResponse>(`${BASE}?${params}`);
  },
  getById: (id: string) => apiGet<SalesInvoiceDto>(`${BASE}/${id}`),
  create: (p: CreateSalesPayload) => apiPost<SalesInvoiceDto>(BASE, p),
  update: (id: string, p: UpdateSalesPayload) =>
    apiPut<SalesInvoiceDto>(`${BASE}/${id}`, p),
  applyDiscount: (id: string, discountPct: number) =>
    apiPost<SalesInvoiceDto>(`${BASE}/${id}/apply-discount`, { discountPct }),
  /** El servidor resuelve el punto de emisión desde ICurrentCashSession — nunca desde el cliente. */
  authorize: (id: string) =>
    apiPost<SalesInvoiceDto>(`${BASE}/${id}/authorize`, {}),
  cancel: (id: string, reason: string) =>
    apiPost<SalesInvoiceDto>(`${BASE}/${id}/cancel`, { reason }),

  listPaymentMethods: (onlyActive = true) =>
    apiGet<PaymentMethodDto[]>(`${PM_BASE}?onlyActive=${onlyActive}`),

  /** Reporte básico de ventas por rango de fechas. Sin fechas, el backend usa el día actual. */
  dailyReport: (dateFrom?: string, dateTo?: string) => {
    const params = new URLSearchParams();
    if (dateFrom) params.set("dateFrom", dateFrom);
    if (dateTo) params.set("dateTo", dateTo);
    const qs = params.toString();
    const url = qs ? `${BASE}/report?${qs}` : `${BASE}/report`;
    return apiGet<SalesReportResponse>(url);
  },
};
