import { apiGet } from "../../lib/apiEnvelope";
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
  | "Discarded";

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

  // ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — sin registro electrónico manual: la transmisión se inicia
  // automáticamente al confirmar el documento origen (con recuperación en el servidor). El endpoint
  // POST /retentions/{id}/electronic/register queda solo como acción de recuperación server-side.
};
