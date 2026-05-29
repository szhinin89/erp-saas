import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export type SriConfigDto = {
  companyId: string;
  certificateP12Path: string;
  environment: number;
  emissionType: number;
  sriAuthorizationUrl: string;
};

export type UpsertSriConfigRequest = {
  certP12Path: string;
  certPassword: string;
  environment: number;
  emissionType: number;
  wsdlUrl: string;
};

export const sriService = {
  get: () =>
    api
      .get<ApiResponse<SriConfigDto | null>>('/api/settings/sri')
      .then((r) => r.data.responseObject ?? null),

  upsert: (body: UpsertSriConfigRequest) =>
    api
      .put<ApiResponse<SriConfigDto>>('/api/settings/sri', body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),
};
