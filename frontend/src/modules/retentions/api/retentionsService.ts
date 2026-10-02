import { apiGet, apiPost } from "../../lib/apiEnvelope";
import { api } from "../../lib/api";

// PURCHASES-RETENTIONS-UI-MIGRATION-05C — cliente transversal del módulo Retentions
// (ERP.Application/Modules/Retentions). Consumido hoy desde Compras (RetentionSection dentro de
// PurchasesPage.tsx) — nunca crea pantalla/menú propio de Retenciones (decisión fija). Tipos
// espejo de los DTOs/records del backend, serializados en camelCase con enums como string (ver
// backend/src/ERP.Application/Modules/Retentions/DTOs/RetentionDocumentDto.cs,
// backend/src/ERP.Application/Modules/Retentions/UseCases/RetentionIntent.cs). Nunca se envía
// TenantId/CompanyId/BranchId en el body — y SourceDocumentType/SourceDocumentId tampoco: el
// documento origen lo fija siempre la ruta de su confirmación.

export type RetentionTaxType = "Vat" | "Income";
export type RetentionStatus = "Draft" | "Issued" | "Cancelled";
export type RetentionSourceDocumentType = "ExpenseDocument" | "PurchaseInvoice" | "Manual";

/**
 * Espejo de `ElectronicDocumentSourceStatus` (backend, ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A):
 * estado electrónico compacto de la retención, calculado en el servidor.
 */
export type RetentionElectronicStatus =
  | "Pending"
  | "Processing"
  | "Authorized"
  | "Rejected"
  | "RequiresReconciliation"
  | "Discarded"
  | "AnnulmentPending"
  | "Annulled";

/**
 * ZH-RETENTION-SRI-ANNULMENT-01/01B — anulación ante el SRI de una retención AUTORIZADA. La SOLICITUD es
 * asistida (el usuario la presenta en SRI en Línea); la VERIFICACIÓN es automática: el backend consulta
 * el WS ConsultaComprobante (Ficha Técnica v2.34 §8). El usuario nunca declara el estado fiscal.
 * Espejo de `RetentionAnnulmentStatus` / `SriStatusQueryOutcome` / `SriFiscalStatus` /
 * `RetentionAnnulmentRequestDto`.
 */
export type RetentionAnnulmentStatus =
  | "PendingSubmission"
  | "PendingSriResolution"
  | "Accepted"
  | "Rejected"
  | "Expired"
  | "Abandoned";

/** Resultado técnico de la consulta a ConsultaComprobante (no es un estado fiscal). */
export type SriStatusQueryOutcome = "Success" | "Rejected" | "Timeout" | "Unavailable" | "Unknown";

/** Estado fiscal informado por el SRI (solo con una consulta `Success`). */
export type SriFiscalStatus = "Unknown" | "Authorized" | "NotAuthorized" | "PendingAnnulment" | "Annulled";

export interface RetentionAnnulmentRequestDto {
  id: string;
  retentionDocumentId: string;
  sourceDocumentType: RetentionSourceDocumentType;
  sourceDocumentId: string;
  status: RetentionAnnulmentStatus;
  reason: string;
  requestedBy: string;
  requestedAtUtc: string;
  accessKey: string;
  retentionNumber: string;
  retentionIssueDate: string;
  receptorIdentification: string;
  receptorName: string;
  ordinaryDeadline: string;
  isPastOrdinaryDeadline: boolean;
  submittedOn: string | null;
  submittedAtUtc: string | null;
  submissionReference: string | null;
  resolvedOn: string | null;
  resolvedAtUtc: string | null;
  evidenceReference: string | null;
  notes: string | null;
  finalizedAtUtc: string | null;
  finalizationAttempts: number;
  lastFinalizationError: string | null;
  requiresFinalization: boolean;
  /** 01B — última consulta a ConsultaComprobante (en línea o automática). */
  lastSriCheckAtUtc: string | null;
  lastSriQueryOutcome: SriStatusQueryOutcome | null;
  lastSriFiscalStatus: SriFiscalStatus | null;
  /** Literal crudo informado por el SRI (p.ej. "PENDIENTE DE ANULAR", "RECHAZADA"). */
  lastSriRawStatus: string | null;
  sriCheckCount: number;
  /** Regla del backend: antes de presentar, o presentada con el SRI confirmando AUTORIZADO. */
  canAbandon: boolean;
}

/** Error de la anulación de Compra/Gasto cuando su retención ya está autorizada por el SRI. */
export const SRI_ANNULMENT_REQUIRED_CODE = "ELECTRONIC_DOCUMENT_REQUIRES_SRI_ANNULMENT";

/** Espejo de `IssueRetentionLineInput` (backend). `retentionCodeDescription` es opcional: el backend usa `retentionCode` como respaldo. */
export interface IssueRetentionLineRequest {
  taxType: RetentionTaxType;
  retentionCode: string;
  baseAmount: number;
  retentionRate: number;
  retainedAmount: number;
  description?: string | null;
  retentionCodeDescription?: string | null;
}

/**
 * Espejo de `RetentionIntent` (backend) — intención opcional de emitir la retención DENTRO de la
 * confirmación del documento origen (Gastos y Compras, RETENTIONS-MODULE-DESIGN-01 decisión 15,
 * ZH-PURCHASE-RETENTION-CONFIRM-01). Nunca incluye el número de retención (lo genera el backend
 * vía secuencia "07" a partir de `emissionPointId`) ni Tenant/Company/Branch.
 */
export interface RetentionIntentRequest {
  appliesRetention: boolean;
  emissionPointId?: string | null;
  issueDate?: string | null;
  lines?: IssueRetentionLineRequest[] | null;
}

export interface RetentionDocumentLineDto {
  id: string;
  taxType: RetentionTaxType;
  retentionCode: string;
  baseAmount: number;
  retentionRate: number;
  retainedAmount: number;
  description: string | null;
  retentionCodeDescription?: string | null;
}

export interface RetentionDocumentDto {
  id: string;
  companyId: string;
  branchId: string;
  sourceDocumentType: RetentionSourceDocumentType;
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
  fiscalPeriod: string | null;
  sourceDocumentSriTypeCode: string | null;
  sourceDocumentNumber: string | null;
  sourceDocumentIssueDate: string | null;
  sourceDocumentAuthorizationNumber: string | null;
  sourceDocumentTaxSupportCode: string | null;
  sourceDocumentSubtotal: number | null;
  sourceDocumentTotal: number | null;
  electronicStatus?: RetentionElectronicStatus | null;
  annulment?: RetentionAnnulmentRequestDto | null;
}

const PURCHASES_BASE = "/api/v1/purchases";
const RETENTIONS_BASE = "/api/v1/retentions";

export const retentionsService = {
  /**
   * Retención transversal activa de una compra, si existe. `null` es un estado normal (todavía
   * no se emitió ninguna), nunca un error.
   */
  getForPurchase: (purchaseInvoiceId: string) =>
    apiGet<RetentionDocumentDto | null>(`${PURCHASES_BASE}/${purchaseInvoiceId}/retention`),

  /** XML de comprobante de retención, on-demand (sin firmar, sin autorizar, sin persistir). */
  async getElectronicXmlBlob(retentionId: string): Promise<Blob> {
    const { data } = await api.get<Blob>(`${RETENTIONS_BASE}/${retentionId}/electronic/xml`, {
      responseType: "blob",
    });
    return data;
  },

  /** RIDE PDF del comprobante de retención, on-demand (mismo criterio que el XML). */
  async getRidePdfBlob(retentionId: string): Promise<Blob> {
    const { data } = await api.get<Blob>(`${RETENTIONS_BASE}/${retentionId}/ride/pdf`, {
      responseType: "blob",
    });
    return data;
  },

  // ── ZH-RETENTION-SRI-ANNULMENT-01 — pasos del trámite (se inicia anulando la Compra/Gasto) ──

  /**
   * "Ya presenté la solicitud" en SRI en Línea (no significa ANULADO). El backend consulta enseguida
   * ConsultaComprobante y devuelve la solicitud con el resultado de esa consulta.
   */
  submitAnnulment: (
    requestId: string,
    body: { submittedOn: string; reference?: string | null; notes?: string | null },
  ) => apiPost<RetentionAnnulmentRequestDto>(`${RETENTIONS_BASE}/annulments/${requestId}/submission`, body),

  /** Consulta el estado en el SRI (ConsultaComprobante) y lo aplica: ANULADO finaliza la anulación. */
  verifyAnnulmentWithSri: (requestId: string) =>
    apiPost<RetentionAnnulmentRequestDto>(`${RETENTIONS_BASE}/annulments/${requestId}/sri-verification`, {}),

  /** Desistir: antes de presentar, o si el SRI confirma que el comprobante sigue AUTORIZADO. */
  abandonAnnulment: (requestId: string, reason: string) =>
    apiPost<RetentionAnnulmentRequestDto>(`${RETENTIONS_BASE}/annulments/${requestId}/abandon`, { reason }),

  /** Reintenta anular el documento origen tras un ANULADO cuya finalización falló. */
  retryAnnulmentFinalization: (requestId: string) =>
    apiPost<RetentionAnnulmentRequestDto>(`${RETENTIONS_BASE}/annulments/${requestId}/finalization`, {}),

  // ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — sin registro electrónico manual: la transmisión se inicia
  // automáticamente al confirmar el documento origen (con recuperación en el servidor). El endpoint
  // POST /retentions/{id}/electronic/register queda solo como acción de recuperación server-side.
};
