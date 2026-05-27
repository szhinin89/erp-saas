import { apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';

export type EstablishmentDto = {
  id: string;
  branchId: string;
  companyId: string;
  code: string;
  name: string;
  address: string;
  phone: string | null;
  isMain: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
};

export type CreateEstablishmentRequest = {
  branchId: string;
  code: string;
  name: string;
  address: string;
  phone?: string | null;
  isMain: boolean;
};

export type UpdateEstablishmentRequest = {
  id: string;
  name: string;
  address: string;
  phone?: string | null;
  isMain: boolean;
};

const base = (branchId: string) => `/api/settings/branches/${branchId}/establishments`;

export const establishmentService = {
  list: (branchId: string) => apiGet<EstablishmentDto[]>(base(branchId)),

  create: (branchId: string, body: CreateEstablishmentRequest) =>
    apiPost<EstablishmentDto>(base(branchId), body),

  update: (branchId: string, id: string, body: UpdateEstablishmentRequest) =>
    apiPut<EstablishmentDto>(`${base(branchId)}/${id}`, body),

  disable: (branchId: string, id: string) =>
    apiPatch<boolean>(`${base(branchId)}/${id}/disable`),

  enable: (branchId: string, id: string) =>
    apiPatch<boolean>(`${base(branchId)}/${id}/enable`),
};
