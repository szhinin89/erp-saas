import { apiGet, apiPatch, apiPost } from "../../lib/apiEnvelope";

// PHYSICAL-FISCAL-F1 — autorización de impresión del SRI por punto de emisión físico y tipo de
// comprobante. TenantId/CompanyId vienen del contexto autenticado en el backend (nunca del body).
// Las fechas son de negocio ("YYYY-MM-DD", ADR-034).
export type PhysicalDocumentAuthorizationDto = {
  id: string;
  emissionPointId: string;
  docTypeCode: string;
  authorizationNumber: string;
  authorizedOn: string;
  expiresOn: string;
  firstSequential: number;
  lastSequential: number;
  isActive: boolean;
};

export type CreatePhysicalDocumentAuthorizationPayload = {
  emissionPointId: string;
  docTypeCode: string;
  authorizationNumber: string;
  authorizedOn: string;
  expiresOn: string;
  firstSequential: number;
  lastSequential: number;
};

const BASE = "/api/v1/settings/physical-document-authorizations";

export const physicalDocumentAuthorizationsService = {
  /** Autorizaciones (activas e inactivas) de la empresa activa. */
  list: () => apiGet<PhysicalDocumentAuthorizationDto[]>(BASE),

  /** Registra la autorización tal como consta en el documento del SRI. 409 si el rango se superpone. */
  create: (body: CreatePhysicalDocumentAuthorizationPayload) =>
    apiPost<PhysicalDocumentAuthorizationDto>(BASE, body),

  /** Deshabilita (soft delete) la autorización. */
  disable: (id: string) => apiPatch<boolean>(`${BASE}/${id}/disable`),
};
