import { apiGet, apiPatch, apiPost, apiPut } from "../../lib/apiEnvelope";

const BASE = "/api/v1/cash-sessions";
const REGISTERS_BASE = "/api/v1/cash-registers";

// ── DTOs ─────────────────────────────────────────────────────────────────

export interface CashMovementDto {
  id: string;
  movementType: string;
  amount: number;
  description: string;
  createdAt: string;
  createdBy: string;
  referenceType: string;
  referenceId: string | null;
  referenceNumber: string | null;
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

export interface CashSessionListItemDto {
  id: string;
  userId: string;
  cashRegisterId: string;
  cashRegisterCodeSnapshot: string;
  emissionPointId: string;
  openedAt: string;
  openingAmount: number;
  status: string;
  currentBalance: number;
  movementCount: number;
  closedAt: string | null;
  difference: number | null;
  createdAt: string;
}

export interface CashSessionListResponse {
  items: CashSessionListItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CashRegisterDto {
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
  branchId: string;
  code: string;
  name: string;
  emissionPointId?: string | null;
  notes?: string | null;
  defaultWarehouseId?: string | null;
  defaultCustomerId?: string | null;
}

export interface UpdateCashRegisterPayload {
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
  amount: number;
  description: string;
  referenceType?: string;
  referenceId?: string;
  referenceNumber?: string;
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
};
