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

import type {
  RetentionDocumentDto,
  RetentionIntentRequest,
} from "../../retentions/facades/retentionDocumentFacade";

/**
 * RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01 — reemplaza los antiguos
 * suggestedVatRetentionCode/suggestedIncomeRetentionCode (máx. 1 código por impuesto) por 0..N
 * candidatos por impuesto — el proveedor puede tener varias retenciones default activas del
 * mismo impuesto.
 */
export interface RetentionEligibilityCandidate {
  taxType: string; // "IVA" | "RENTA"
  retentionCode: string;
  retentionCodeName: string;
  retentionPct: number;
}

export interface RetentionEligibilityResult {
  canRetainVat: boolean;
  canRetainIncome: boolean;
  isSupplierExempt: boolean;
  hasRetainableBase: boolean;
  missingRetentionCode: boolean;
  isSupplierRequiredToKeepAccounting: boolean;
  candidates: RetentionEligibilityCandidate[];
  reasons: string[];
  /** Propiedad calculada del record C# (CanRetainVat || CanRetainIncome) — también serializada. */
  isEligible: boolean;
}

/**
 * ZH-PURCHASE-RETENTION-CONFIRM-01 — contrato único de la intención de retener al confirmar
 * (Gastos y Compras), declarado en el módulo Retentions; aquí solo se reexporta con los nombres que
 * Gastos ya usaba. El número de retención nunca viaja: lo genera el backend (secuencia "07").
 */
export type {
  IssueRetentionLineRequest as RetentionIntentLineRequest,
  RetentionIntentRequest,
} from "../../retentions/facades/retentionDocumentFacade";

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
  /**
   * RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H — código de sustento tributario SRI (codSustento,
   * catálogo `global.sri_tax_support`). Espejo de `ExpenseDocument.TaxSupportCode` (backend,
   * RETENTIONS-SOURCE-DOCUMENT-TAX-SUPPORT-02G) — se copia al snapshot de la retención al emitir.
   */
  taxSupportCode: string | null;
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
  receptionDocumentId?: string | null;
  accessKey?: string | null;
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
  /**
   * RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H — opcional; si no se envía (o llega vacío/null), el
   * backend usa el default configurable del proveedor (`SupplierRoleConfig.DefaultTaxSupportCode`)
   * — ver `ExpenseDraftRules.ResolveTaxSupportCode`. Reutilizado tal cual por
   * `CreateExpenseDraftPayload`/`UpdateExpenseDraftPayload`/`createConfirmedExpense`.
   */
  taxSupportCode?: string | null;
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
