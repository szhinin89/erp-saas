import { apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';

// ── Enums ──────────────────────────────────────────────────────────────────
export type EmissionType = 'Electronic' | 'Physical';
export const EMISSION_TYPE_ELECTRONIC: EmissionType = 'Electronic';
export const EMISSION_TYPE_PHYSICAL: EmissionType = 'Physical';

// ── DTOs ──────────────────────────────────────────────────────────────────
/** Item para el listado principal de la pantalla independiente. */
export type EmissionPointListItemDto = {
  id: string;
  establishmentId: string;
  establishmentCode: string;
  establishmentName: string;
  branchName: string | null;
  code: string;
  name: string | null;
  emissionType: EmissionType;
  isDefault: boolean;
  isActive: boolean;
  createdAt: string;
};

/** DTO devuelto en operaciones de creación / actualización. */
export type EmissionPointDto = {
  id: string;
  establishmentId: string;
  companyId: string;
  code: string;
  name: string | null;
  emissionType: EmissionType;
  isDefault: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
};

/** Lookup para poblar el selector de Establecimiento en el formulario. */
export type EstablishmentLookupDto = {
  id: string;
  code: string;
  name: string;
};

// ── Payloads ──────────────────────────────────────────────────────────────
export type CreateEmissionPointPayload = {
  establishmentId: string;
  code: string;
  name?: string | null;
  emissionType: EmissionType;
  isDefault: boolean;
};

export type UpdateEmissionPointPayload = {
  id: string;
  name?: string | null;
  emissionType: EmissionType;
  isDefault: boolean;
};

// ── Service ───────────────────────────────────────────────────────────────
const BASE = '/api/v1/settings/emission-points';

export const emissionPointsService = {
  /** Lista todos los puntos de emisión de la empresa activa con filtros opcionales. */
  list: (activeStatus: 'all' | 'active' | 'inactive' = 'active', search?: string) =>
    apiGet<EmissionPointListItemDto[]>(
      `${BASE}?activeStatus=${activeStatus}${search ? `&search=${encodeURIComponent(search)}` : ''}`,
    ),

  /** Establecimientos activos para poblar el selector del formulario. */
  establishmentLookups: () =>
    apiGet<EstablishmentLookupDto[]>('/api/v1/settings/establishments/lookups'),

  create: (body: CreateEmissionPointPayload) =>
    apiPost<EmissionPointDto>(BASE, body),

  update: (id: string, body: UpdateEmissionPointPayload) =>
    apiPut<EmissionPointDto>(`${BASE}/${id}`, body),

  disable: (id: string) =>
    apiPatch<boolean>(`${BASE}/${id}/disable`),

  enable: (id: string) =>
    apiPatch<boolean>(`${BASE}/${id}/enable`),
};
