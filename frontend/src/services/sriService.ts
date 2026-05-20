import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type SriConfigDto = {
  subscriberId: string;
  companyRuc: string;
  legalName: string;
  tradeName: string | null;
  mainAddress: string;
  requiresAccounting: boolean;
  specialTaxpayer: string | null;
  estabCode: string;
  emPointCode: string;
  currentSequential: number;
  certificateP12Path: string;
  environment: number;
  emissionType: number;
  sriAuthorizationUrl: string;
};

export type UpsertSriConfigRequest = {
  ruc: string;
  legalName: string;
  tradeName?: string | null;
  mainAddress: string;
  requiresAccounting: boolean;
  specialTaxpayer?: string | null;
  estabCode: string;
  emPointCode: string;
  certP12Path: string;
  certPassword: string;
  environment: number;
  emissionType: number;
  wsdlUrl: string;
};

export const sriService = {
  get: () =>
    api
      .get<ApiResponse<SriConfigDto | null>>('/api/configuracion-sri')
      .then((r) => r.data.responseObject ?? null),

  upsert: (body: UpsertSriConfigRequest) =>
    api
      .put<ApiResponse<SriConfigDto>>('/api/configuracion-sri', body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),
};
