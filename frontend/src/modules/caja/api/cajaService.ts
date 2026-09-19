import { apiGet, apiPatch, apiPost, apiPut } from "../../lib/apiEnvelope";

const BASE = "/api/v1/cash-sessions";
const REGISTERS_BASE = "/api/v1/cash-registers";
const REASONS_BASE = "/api/v1/cash-movement-reasons";

// ── DTOs ─────────────────────────────────────────────────────────────────

/** TREASURY-CASH-MANUAL-MOVEMENTS-01 — reasonId/reasonName null en movimientos de sistema
 * (Opening/SaleIncome/SaleRefund) y en histórico anterior a este ticket; createdByName puede ser
 * null si el usuario ya no existe. */
export interface CashMovementDto {
  id: string;
  movementType: string;
  amount: number;
  description: string;
  createdAt: string;
  createdBy: string;
  createdByName: string | null;
  referenceType: string;
  referenceId: string | null;
  referenceNumber: string | null;
  reasonId: string | null;
  reasonName: string | null;
}

// ── TREASURY-CASH-MANUAL-MOVEMENTS-01 ───────────────────────────────────
// Catálogo administrable de motivos de movimiento manual de caja — SSOT dinámico, scope
// obligatorio Tenant+Company. Consumido por el select "Motivo" del formulario de registro manual.

export interface CashMovementReasonDto {
  id: string;
  code: string;
  name: string;
  /** "ManualIncome" | "ManualExpense" | "Withdrawal" — nunca un tipo de sistema. */
  movementType: string;
  isActive: boolean;
  sortOrder: number;
}

export interface CashClosingCountDto {
  id: string;
  denominationValue: number;
  denominationLabel: string;
  quantity: number;
  total: number;
}

export interface CashSessionDto {
  id: string;
  companyId: string;
  branchId: string;
  userId: string;
  cashRegisterId: string;
  cashRegisterCodeSnapshot: string;
  cashRegisterNameSnapshot: string;
  emissionPointId: string;
  emissionPointCodeSnapshot: string;
  /** 'Electronic' | 'Physical' — resuelto en vivo por el backend desde EmissionPoint.EmissionType
   * (nunca un snapshot); null si el punto de emisión ya no existe/está activo. */
  emissionType: string | null;
  /** Bodega/cliente por defecto de la Caja dueña de la sesión — conveniencias de pre-llenado para
   * Ventas, resueltas en vivo por el backend desde CashRegister.DefaultWarehouseId/DefaultCustomerId. */
  defaultWarehouseId: string | null;
  defaultWarehouseName: string | null;
  defaultCustomerId: string | null;
  defaultCustomerName: string | null;
  openedAt: string;
  openingAmount: number;
  status: string;
  notes: string | null;
  closedAt: string | null;
  closedBy: string | null;
  closeNotes: string | null;
  expectedAmount: number | null;
  countedAmount: number | null;
  difference: number | null;
  totalIncome: number;
  totalExpense: number;
  currentBalance: number;
  movements: CashMovementDto[];
  closingCounts: CashClosingCountDto[];
  createdAt: string;
  updatedAt: string | null;
}

/// CASH-SESSION-LIST-SUMMARY-01 — SSOT sin cambios: currentBalance/expectedCash/countedAmount/
/// difference vienen de CashSession/CashMovement (efectivo físico); invoiceCount/totalInvoiced/
/// byPaymentMethod vienen de SalesInvoice+SalesInvoicePayment+PaymentMethod (informativo, nunca
/// afecta el efectivo físico ni se persiste).
export interface CashSessionListCollectionByMethodDto {
  paymentMethodId: string;
  paymentMethodCode: string;
  paymentMethodName: string;
  isCreditAllowed: boolean;
  invoiceCount: number;
  amount: number;
}

export interface CashSessionListItemDto {
  id: string;
  userId: string;
  userName: string | null;
  cashRegisterId: string;
  cashRegisterCodeSnapshot: string;
  cashRegisterNameSnapshot: string;
  emissionPointId: string;
  emissionPointCodeSnapshot: string;
  openedAt: string;
  openingAmount: number;
  status: string;
  currentBalance: number;
  /** Efectivo físico esperado ahora: currentBalance si sigue abierto, o el ExpectedAmount congelado al cierre. */
  expectedCash: number;
  /** Solo turnos cerrados. */
  countedAmount: number | null;
  movementCount: number;
  closedAt: string | null;
  closedBy: string | null;
  closedByName: string | null;
  difference: number | null;
  /** Facturas AUTORIZADAS del turno (Draft/Cancelled excluidas). */
  invoiceCount: number;
  totalInvoiced: number;
  /** Solo ventas en Efectivo que movieron el cajón físico — subconjunto de totalInvoiced. */
  saleIncomeCash: number;
  manualIncomeCash: number;
  /** ManualExpense + Withdrawal — no incluye SaleRefund (reverso automático, no acción manual). */
  manualExpenseCash: number;
  byPaymentMethod: CashSessionListCollectionByMethodDto[];
  createdAt: string;
}

export interface CashSessionListResponse {
  items: CashSessionListItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CashRegisterDto {
  accountingAccountId: string | null;
  id: string;
  branchId: string;
  branchName: string;
  branchCode: string | null;
  emissionPointId: string | null;
  establishmentCode: string | null;
  emissionPointCode: string | null;
  emissionPointName: string | null;
  code: string;
  name: string;
  notes: string | null;
  isActive: boolean;
  /** true si la caja ya tiene historial operativo (aperturas/cierres/movimientos) — calculado
   * siempre por el backend (ICashRegisterUsageGuard). Único indicador para bloquear
   * Código/Sucursal/Punto de emisión en el formulario de edición; nunca inferir en el frontend. */
  hasHistory: boolean;
  /** Bodega/cliente preseleccionados al iniciar una venta desde esta caja — conveniencias
   * operativas, siempre editables (a diferencia de Código/Sucursal/Punto de Emisión). */
  defaultWarehouseId: string | null;
  defaultWarehouseCode: string | null;
  defaultWarehouseName: string | null;
  defaultCustomerId: string | null;
  defaultCustomerName: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface EmissionPointLookupForBranchDto {
  id: string;
  code: string;
  name: string | null;
  establishmentCode: string;
}

export type CashRegisterActiveStatus = "all" | "active" | "inactive";

export interface CreateCashRegisterPayload {
  accountingAccountId?: string | null;
  branchId: string;
  code: string;
  name: string;
  emissionPointId?: string | null;
  notes?: string | null;
  defaultWarehouseId?: string | null;
  defaultCustomerId?: string | null;
}

export interface UpdateCashRegisterPayload {
  accountingAccountId?: string | null;
  id: string;
  name: string;
  emissionPointId?: string | null;
  notes?: string | null;
  defaultWarehouseId?: string | null;
  defaultCustomerId?: string | null;
}

// ── Payloads ─────────────────────────────────────────────────────────────

/** El cliente nunca envía BranchId/CompanyId/TenantId/EmissionPointId — el servidor los resuelve
 * desde la caja (CashRegister) elegida y el contexto de sucursal activo. */
export interface OpenCashSessionPayload {
  cashRegisterId: string;
  openingAmount: number;
  notes?: string;
}

export interface CashClosingCountInput {
  denominationValue: number;
  denominationLabel: string;
  quantity: number;
}

export interface CloseCashSessionPayload {
  closingCounts: CashClosingCountInput[];
  closeNotes?: string;
}

export interface RecordMovementPayload {
  movementType: string;
  reasonId: string;
  amount: number;
  description: string;
  referenceType?: string;
  referenceId?: string;
  referenceNumber?: string;
}

// ── CASH-SESSION-COLLECTION-SUMMARY-01 / UX-02 ──────────────────────────
// Resumen informativo de ventas/cobros del turno — separado del efectivo físico de
// CashSessionDto (totalIncome/currentBalance arriba). "Cobros del turno" != "efectivo físico de
// caja": una venta por Transferencia/Tarjeta/Cheque/Crédito aparece aquí pero nunca en
// totalIncome/currentBalance. UX-02 solo agrega profundidad informativa (código, operaciones,
// destino, detalle de transferencia) — ningún campo participa en el cálculo de efectivo físico.

export interface CashSessionCollectionDetailDto {
  invoiceId: string;
  invoiceNumber: string;
  authorizedAt: string;
  customerName: string;
  /** GrandTotal de la factura — para contrastar contra `amount` cuando la venta fue mixta. */
  invoiceTotal: number;
  /** Monto de ESTA forma de pago en ESTA factura — nunca el total si la venta fue mixta. */
  amount: number;
  /** true si la factura tiene más de un pago (de cualquier forma) — "Completa" vs "Mixta". */
  isMixedPayment: boolean;
  /** Referencia/comprobante genérico; para Transferencia es el comprobante bancario real. */
  reference: string | null;
  /** Solo Transferencia — nombre real del banco (o texto legacy en operaciones anteriores). */
  destinationBankName: string | null;
  /** Solo Transferencia — alias + cuenta enmascarada (últimos 4 dígitos). */
  destinationAccountMasked: string | null;
  /** Solo Transferencia — comprobante de la operación bancaria. */
  transferReceiptNumber: string | null;
  /** Solo Transferencia — fecha de la operación bancaria (YYYY-MM-DD). */
  transferDate: string | null;
}

export interface CashSessionCollectionByMethodDto {
  paymentMethodId: string;
  paymentMethodCode: string;
  paymentMethodName: string;
  isCreditAllowed: boolean;
  /** Facturas distintas que usaron esta forma. */
  invoiceCount: number;
  /** Líneas de pago (operaciones) con esta forma — puede superar invoiceCount en venta mixta con 2 pagos de la misma forma. */
  operationCount: number;
  amount: number;
  /** Etiqueta contable genérica ("Caja física", "Cuenta bancaria", "Cuenta configurada", "Cuentas por Cobrar") — nunca una cuenta real inventada. */
  destination: string;
  details: CashSessionCollectionDetailDto[];
}

export interface CashSessionCollectionSummaryDto {
  invoiceCount: number;
  totalInvoiced: number;
  totalCollected: number;
  totalCredit: number;
  byPaymentMethod: CashSessionCollectionByMethodDto[];
}

// ── Service ──────────────────────────────────────────────────────────────

export const cajaService = {
  /** Solo devuelve cajas de la sucursal activa — el servidor resuelve BranchId desde ICurrentBranch. */
  getCashRegisters: (activeOnly?: boolean) => {
    const params = new URLSearchParams();
    if (activeOnly !== undefined) params.set("activeOnly", String(activeOnly));
    const qs = params.toString();
    return apiGet<CashRegisterDto[]>(`${REGISTERS_BASE}${qs ? `?${qs}` : ""}`);
  },

  getCashRegister: (id: string) =>
    apiGet<CashRegisterDto>(`${REGISTERS_BASE}/${id}`),

  /** Administración de Cajas: listado empresa-completa (todas las sucursales). */
  listAllCashRegisters: (
    activeStatus: CashRegisterActiveStatus = "all",
    search?: string,
  ) => {
    const params = new URLSearchParams();
    if (activeStatus !== "all")
      params.set("activeOnly", String(activeStatus === "active"));
    if (search?.trim()) params.set("search", search.trim());
    const qs = params.toString();
    return apiGet<CashRegisterDto[]>(
      `${REGISTERS_BASE}/all${qs ? `?${qs}` : ""}`,
    );
  },

  emissionPointLookupsByBranch: (branchId: string) =>
    apiGet<EmissionPointLookupForBranchDto[]>(
      `${REGISTERS_BASE}/emission-point-lookups?branchId=${branchId}`,
    ),

  createCashRegister: (payload: CreateCashRegisterPayload) =>
    apiPost<CashRegisterDto>(REGISTERS_BASE, payload),

  updateCashRegister: (id: string, payload: UpdateCashRegisterPayload) =>
    apiPut<CashRegisterDto>(`${REGISTERS_BASE}/${id}`, payload),

  disableCashRegister: (id: string) =>
    apiPatch<boolean>(`${REGISTERS_BASE}/${id}/disable`),

  enableCashRegister: (id: string) =>
    apiPatch<boolean>(`${REGISTERS_BASE}/${id}/enable`),

  list: (status?: string, page = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (status?.trim()) params.set("status", status.trim());
    params.set("pageNumber", String(page));
    params.set("pageSize", String(pageSize));
    return apiGet<CashSessionListResponse>(`${BASE}?${params}`);
  },

  getById: (id: string) => apiGet<CashSessionDto>(`${BASE}/${id}`),

  getMy: () => apiGet<CashSessionDto | null>(`${BASE}/my`),

  open: (p: OpenCashSessionPayload) =>
    apiPost<CashSessionDto>(`${BASE}/open`, p),

  close: (id: string, p: CloseCashSessionPayload) =>
    apiPost<CashSessionDto>(`${BASE}/${id}/close`, p),

  recordMovement: (id: string, p: RecordMovementPayload) =>
    apiPost<CashMovementDto>(`${BASE}/${id}/movements`, p),

  getCollectionSummary: (id: string) =>
    apiGet<CashSessionCollectionSummaryDto>(`${BASE}/${id}/collection-summary`),

  /** TREASURY-CASH-MANUAL-MOVEMENTS-01 — motivos activos filtrados por tipo de movimiento
   * (Tenant+Company siempre resueltos server-side, nunca enviados por el cliente). */
  getCashMovementReasons: (movementType: string) =>
    apiGet<CashMovementReasonDto[]>(
      `${REASONS_BASE}?movementType=${encodeURIComponent(movementType)}`,
    ),
};
