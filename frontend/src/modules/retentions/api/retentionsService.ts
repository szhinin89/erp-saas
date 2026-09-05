import { apiGet, apiPost } from "../../lib/apiEnvelope";
import { api } from "../../lib/api";

// PURCHASES-RETENTIONS-UI-MIGRATION-05C — cliente transversal del módulo Retentions
// (ERP.Application/Modules/Retentions). Consumido hoy desde Compras (RetentionSection dentro de
// PurchasesPage.tsx) — nunca crea pantalla/menú propio de Retenciones (decisión fija). Tipos
// espejo de los DTOs/records del backend, serializados en camelCase con enums como string (ver
// backend/src/ERP.Application/Modules/Retentions/DTOs/RetentionDocumentDto.cs,
// backend/src/ERP.Application/Modules/Retentions/UseCases/IssueRetentionUseCases.cs). Nunca se
// envía TenantId/CompanyId/BranchId en el body — y SourceDocumentType/SourceDocumentId tampoco:
// para Compras, ambos quedan fijos por el endpoint/ruta (PurchaseInvoice + el id de la compra).

export type RetentionTaxType = "Vat" | "Income";
export type RetentionStatus = "Draft" | "Issued" | "Cancelled";
export type RetentionSourceDocumentType = "ExpenseDocument" | "PurchaseInvoice" | "Manual";

export interface IssueRetentionLineRequest {
  taxType: RetentionTaxType;
  retentionCode: string;
  baseAmount: number;
  retentionRate: number;
  retainedAmount: number;
  description?: string | null;
  retentionCodeDescription?: string | null;
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
}

export interface IssuePurchaseRetentionPayload {
  emissionPointId: string;
  issueDate: string;
  lines: IssueRetentionLineRequest[];
}

/** Espejo de ERP.Application.Modules.ElectronicDocuments.DTOs.ElectronicDocumentDto — devuelto por el registro electrónico manual. */
export interface ElectronicDocumentDto {
  id: string;
  documentType: string;
  sourceModule: string;
  sourceEntityId: string;
  currentState: string;
  accessKey: string | null;
  authorizationNumber: string | null;
  authorizationDate: string | null;
  retryCount: number;
  lastAttemptUtc: string | null;
  createdAt: string;
  updatedAt: string | null;
}

const PURCHASES_BASE = "/api/v1/purchases";
const RETENTIONS_BASE = "/api/v1/retentions";

export const retentionsService = {
  /**
   * Retención transversal activa de una compra, si existe — reemplaza
   * `purchaseService.getWithholding` para el flujo nuevo. `null` es un estado normal (todavía no
   * se emitió ninguna), nunca un error.
   */
  getForPurchase: (purchaseInvoiceId: string) =>
    apiGet<RetentionDocumentDto | null>(`${PURCHASES_BASE}/${purchaseInvoiceId}/retention`),

  /**
   * Emite la retención vía el modelo transversal `RetentionDocument` — reemplaza
   * `purchaseService.issueWithholding`. Nunca envía `retentionNumber`/`sourceDocumentType`/
   * `sourceDocumentId` (el backend los fija: número server-side, origen por la ruta).
   */
  issueForPurchase: (purchaseInvoiceId: string, payload: IssuePurchaseRetentionPayload) =>
    apiPost<RetentionDocumentDto>(`${PURCHASES_BASE}/${purchaseInvoiceId}/retention`, payload),

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

  /** Registro electrónico real (firma + SOAP + autorización) — manual y explícito, nunca automático. */
  registerElectronic: (retentionId: string) =>
    apiPost<ElectronicDocumentDto>(`${RETENTIONS_BASE}/${retentionId}/electronic/register`, {}),
};
