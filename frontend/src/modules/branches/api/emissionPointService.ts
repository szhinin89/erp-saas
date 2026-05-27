import { apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';

export type EmissionPointDto = {
  id: string;
  establishmentId: string;
  companyId: string;
  code: string;
  name: string | null;
  isDefault: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
};

export type CreateEmissionPointRequest = {
  establishmentId: string;
  code: string;
  name?: string | null;
  isDefault: boolean;
};

export type UpdateEmissionPointRequest = {
  id: string;
  name?: string | null;
  isDefault: boolean;
};

const base = (establishmentId: string) =>
  `/api/settings/establishments/${establishmentId}/emission-points`;

export const emissionPointService = {
  list: (establishmentId: string) => apiGet<EmissionPointDto[]>(base(establishmentId)),

  create: (establishmentId: string, body: CreateEmissionPointRequest) =>
    apiPost<EmissionPointDto>(base(establishmentId), body),

  update: (establishmentId: string, id: string, body: UpdateEmissionPointRequest) =>
    apiPut<EmissionPointDto>(`${base(establishmentId)}/${id}`, body),

  disable: (establishmentId: string, id: string) =>
    apiPatch<boolean>(`${base(establishmentId)}/${id}/disable`),

  enable: (establishmentId: string, id: string) =>
    apiPatch<boolean>(`${base(establishmentId)}/${id}/enable`),
};
