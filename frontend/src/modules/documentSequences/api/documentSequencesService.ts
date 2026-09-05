import { apiGet, apiPut } from "../../lib/apiEnvelope";

// DOCUMENT-SEQUENCES-CONFIG-UI-04 — DocumentSequence se identifica por (TenantId, CompanyId,
// EmissionPointId, DocTypeCode); TenantId/CompanyId vienen del contexto autenticado en el backend
// (nunca del body). BranchId y Environment NO forman parte de la clave — este DTO/payload nunca
// los incluye.
export type DocumentSequenceDto = {
  emissionPointId: string;
  docTypeCode: string;
  nextNumber: number;
  hasBeenUsed: boolean;
  updatedAt: string;
};

export type ConfigureDocumentSequencePayload = {
  emissionPointId: string;
  docTypeCode: string;
  nextNumber: number;
};

const BASE = "/api/v1/settings/document-sequences";

export const documentSequencesService = {
  /** Lista todas las secuencias documentales ya configuradas/usadas de la empresa activa. */
  list: () => apiGet<DocumentSequenceDto[]>(BASE),

  /** Configura el próximo secuencial, antes de su primer uso real. 409 si ya fue usada. */
  configure: (body: ConfigureDocumentSequencePayload) =>
    apiPut<DocumentSequenceDto>(`${BASE}/configure`, body),
};
