import { apiGet, apiPost } from '../../../lib/apiEnvelope';
import type {
  ElectronicDocumentDiagnosticDto,
  ElectronicDocumentTimelineEventDto,
  ElectronicDocumentXmlVariant,
} from '../../../../components/zh/electronicDocuments/electronicDocumentDiagnosticTypes';

export type { ElectronicDocumentTimelineEventDto, ElectronicDocumentXmlVariant };

export type ElectronicDocumentListItemDto = {
  id: string;
  createdAt: string;
  documentType: string;
  documentNumber: string | null;
  companyId: string;
  companyName: string;
  counterpartyName: string | null;
  currentState: string;
  environment: string | null;
  accessKey: string | null;
  retryCount: number;
  updatedAt: string | null;
  lastMessage: string | null;
};

export type ElectronicDocumentsListResponse = {
  items: ElectronicDocumentListItemDto[];
  total: number;
  pageNumber: number;
  pageSize: number;
};

export type ElectronicDocumentsListParams = {
  dateFrom?: string;
  dateTo?: string;
  /** Uno o más estados separados por coma (p.ej. "Signed,Sent,Received"). */
  state?: string;
  documentType?: string;
  environment?: string;
  search?: string;
  pageNumber?: number;
  pageSize?: number;
};

/**
 * Detalle de Monitor — datos propios de esta pantalla (compañía, documento de origen) más el
 * `diagnostic` genérico y reutilizable que también consumen otros módulos (Ventas, etc.) vía
 * `electronicDocumentDiagnosticService.getDiagnosticBySource(...)`. No duplica campos que ya
 * vienen en `diagnostic` (antes existían sueltos aquí: error/timeline/xmlXxxAvailable).
 */
export type ElectronicDocumentDetailDto = {
  id: string;
  companyId: string;
  companyName: string;
  companyTaxId: string;
  documentType: string;
  sourceModule: string;
  sourceEntityId: string;
  documentNumber: string | null;
  counterpartyName: string | null;
  createdAt: string;
  updatedAt: string | null;
  observations: string | null;
  diagnostic: ElectronicDocumentDiagnosticDto;
};

export type ElectronicDocumentsDashboardDto = {
  pending: number;
  signed: number;
  sent: number;
  authorized: number;
  rejected: number;
  error: number;
  authorizedToday: number;
  errorsToday: number;
  averageAuthorizationMinutes: number | null;
  pendingRetries: number;
  totalToday: number;
};

export const electronicDocumentsMonitorService = {
  getList: (params: ElectronicDocumentsListParams) =>
    apiGet<ElectronicDocumentsListResponse>('/api/v1/electronic-documents', { params }),

  getDashboard: () =>
    apiGet<ElectronicDocumentsDashboardDto>('/api/v1/electronic-documents/dashboard'),

  getDetail: (id: string) =>
    apiGet<ElectronicDocumentDetailDto>(`/api/v1/electronic-documents/${id}`),

  getTimeline: (id: string) =>
    apiGet<ElectronicDocumentTimelineEventDto[]>(`/api/v1/electronic-documents/${id}/timeline`),

  /** Solo lee el XML ya almacenado (borrador o firmado) — nunca genera uno nuevo. */
  getXml: (sourceModule: string, sourceEntityId: string, variant: ElectronicDocumentXmlVariant) =>
    apiGet<string>('/api/v1/electronic-documents/xml', {
      params: { sourceModule, sourceEntityId, variant },
    }),

  /** Reintenta manualmente un documento varado (Signed/Received/Failed), o lo reactiva desde DeadLetter y reintenta. */
  retryNow: (id: string) =>
    apiPost<ElectronicDocumentDetailDto>(`/api/v1/electronic-documents/${id}/retry`, {}),

  /**
   * Backfill: registra el documento electrónico de un documento de origen ya autorizado
   * comercialmente que nunca generó uno. Idempotente — falla con Conflict si ya existe uno
   * más allá de Draft/Failed.
   */
  register: (documentType: string, sourceModule: string, sourceEntityId: string) =>
    apiPost<{ id: string; currentState: string; accessKey: string | null }>(
      '/api/v1/electronic-documents/register',
      { documentType, sourceModule, sourceEntityId },
    ),
};
