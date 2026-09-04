import { apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";
import { api } from "../../lib/api";

const BASE = "/api/v1/expenses/documents";

export type ExpenseStatus = "Draft" | "Confirmed" | "Cancelled";

// ── RETENTIONS-UI-EXPENSES-01F ──────────────────────────────────────────────
// Tipos espejo de los DTOs/records del backend (ver
// backend/src/ERP.Application/Modules/Retentions/Services/IRetentionEligibilityService.cs,
// backend/src/ERP.Application/Modules/Retentions/DTOs/RetentionDocumentDto.cs,
// backend/src/ERP.Application/Modules/Expenses/DTOs/ExpenseDocumentDraftDtos.cs).
// Serializados en camelCase por la configuración por defecto de ASP.NET Core
// (System.Text.Json), con enums como string vía JsonStringEnumConverter registrado
// en ERP.API/Program.cs — nunca se envía TenantId/CompanyId/BranchId en el body.

/** RetentionTaxType (backend) — nombres de enum tal cual, sin traducir en el contrato de API. */
export type RetentionTaxType = "Vat" | "Income";

/** RetentionStatus (backend). */
export type RetentionStatus = "Draft" | "Issued" | "Cancelled";

export interface RetentionEligibilityResult {
  canRetainVat: boolean;
  canRetainIncome: boolean;
  isSupplierExempt: boolean;
  hasRetainableBase: boolean;
  missingRetentionCode: boolean;
  isSupplierRequiredToKeepAccounting: boolean;
  suggestedVatRetentionCode: string | null;
  suggestedIncomeRetentionCode: string | null;
  reasons: string[];
  /** Propiedad calculada del record C# (CanRetainVat || CanRetainIncome) — también serializada. */
  isEligible: boolean;
}

export interface RetentionIntentLineRequest {
  taxType: RetentionTaxType;
  retentionCode: string;
  baseAmount: number;
  retentionRate: number;
  retainedAmount: number;
  description?: string | null;
  /**
   * RETENTIONS-TAX-COMPONENT-MODEL-02B — snapshot opcional del texto del código de retención.
   * Sin selector de catálogo real en este formulario todavía, se omite; el backend usa
   * `retentionCode` como respaldo cuando no se envía (ver RetentionIssuer.cs).
   */
  retentionCodeDescription?: string | null;
}

/**
 * RETENTIONS-UI-REMOVE-MANUAL-NUMBER-02F — ya NO incluye un número de retención manual: el
 * backend lo genera siempre server-side vía `DocumentSequence.CaptureNextAsync(..., "07")` a
 * partir de `emissionPointId` (ver RETENTIONS-DOCUMENT-SEQUENCE-02E) — enviarlo desde aquí sería
 * un campo fantasma que el backend ya ignora en silencio.
 */
export interface RetentionIntentRequest {
  appliesRetention: boolean;
  emissionPointId?: string | null;
  issueDate?: string | null;
  lines?: RetentionIntentLineRequest[] | null;
}

export interface RetentionDocumentLineDto {
  id: string;
  taxType: RetentionTaxType;
  retentionCode: string;
  baseAmount: number;
  retentionRate: number;
  retainedAmount: number;
  description: string | null;
  /** RETENTIONS-TAX-COMPONENT-MODEL-02B — snapshot del texto del código (nulo en líneas emitidas antes de esta fase). */
  retentionCodeDescription?: string | null;
}

export interface RetentionDocumentDto {
  id: string;
  companyId: string;
  branchId: string;
  sourceDocumentType: string;
  sourceDocumentId: string;
  subjectBusinessPartnerId: string;
  emissionPointId: string;
  retentionNumber: string | null;
  issueDate: string | null;
  status: RetentionStatus;
  totalRetainedVat: number;
  totalRetainedIncome: number;
  totalRetained: number;
  cancelReason: string | null;
  cancelledAt: string | null;
  cancelledBy: string | null;
  lines: RetentionDocumentLineDto[];
  /**
   * RETENTIONS-TAX-COMPONENT-MODEL-02B — periodo fiscal `mm/aaaa` (derivado, nulo en Draft) y
   * snapshot del documento sustento. Todos nulos en retenciones emitidas antes de esta fase.
   */
  fiscalPeriod?: string | null;
  sourceDocumentSriTypeCode?: string | null;
  sourceDocumentNumber?: string | null;
  sourceDocumentIssueDate?: string | null;
  sourceDocumentAuthorizationNumber?: string | null;
  sourceDocumentTaxSupportCode?: string | null;
  sourceDocumentSubtotal?: number | null;
  sourceDocumentTotal?: number | null;
}

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
  cancelReason: string | null;
  cancelledAt: string | null;
  cancelledBy: string | null;
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

  /**
   * RETENTIONS-UI-EXPENSES-01F — `retention` es opcional y por defecto `undefined`: llamadas
   * existentes `confirm(id)` siguen enviando exactamente el mismo body `{}` que antes de esta
   * fase (comportamiento preservado, sin retención). Solo se agrega la clave `retention` al
   * body cuando el llamador la provee explícitamente.
   */
  confirm: (id: string, retention?: RetentionIntentRequest) =>
    apiPost<ExpenseDocumentDetailDto>(
      `${BASE}/${id}/confirm`,
      retention ? { retention } : {},
    ),

  /**
   * RETENTIONS-UI-EXPENSES-01F — POST /expenses/documents/confirmed (crea el gasto ya
   * Confirmed, sin pasar por Draft). `retention` es opcional, mismo criterio que `confirm`.
   */
  createConfirmedExpense: (
    payload: CreateExpenseDraftPayload,
    retention?: RetentionIntentRequest,
  ) =>
    apiPost<ExpenseDocumentDetailDto>(`${BASE}/confirmed`, {
      ...payload,
      ...(retention ? { retention } : {}),
    }),

  /** Confirmed → Cancelled. `reason` es obligatorio en el contrato del backend. */
  cancel: (id: string, reason: string) =>
    apiPost<ExpenseDocumentDetailDto>(`${BASE}/${id}/cancel`, { reason }),

  /**
   * RETENTIONS-ELIGIBILITY-01 — solo lectura, reevaluada siempre por el servidor antes de
   * confirmar. El resultado mostrado en UI es informativo, nunca la fuente final de verdad.
   */
  getRetentionEligibility: (expenseDocumentId: string) =>
    apiGet<RetentionEligibilityResult>(
      `${BASE}/${expenseDocumentId}/retention-eligibility`,
    ),

  /**
   * RETENTIONS-API-EXPENSES-01E — la retención activa asociada al gasto, si existe. 404 es un
   * estado normal ("sin retención"), no un error — se traduce a `null` aquí para que el
   * llamador nunca necesite distinguir "error de red" de "no existe retención".
   */
  getExpenseRetention: async (
    expenseDocumentId: string,
  ): Promise<RetentionDocumentDto | null> => {
    try {
      const { data } = await api.get<
        { data: RetentionDocumentDto } | RetentionDocumentDto
      >(`${BASE}/${expenseDocumentId}/retention`);
      return (data && typeof data === "object" && "data" in data
        ? data.data
        : data) as RetentionDocumentDto;
    } catch (err) {
      const status = (err as { response?: { status?: number } })?.response
        ?.status;
      if (status === 404) return null;
      throw err;
    }
  },
};
