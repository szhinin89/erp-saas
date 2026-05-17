import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type BillingSettingsDto = {
  id: string;
  tenantId: string;
  legalName: string;
  tradeName: string;
  ruc: string;
  mainAddress: string;
  phone: string;
  email: string | null;
  requiresAccounting: boolean;
  specialTaxpayer: string | null;
  logoBase64: string | null;
  additionalNote: string | null;
  receiptWidth: number;
};

export type UpsertBillingSettingsRequest = {
  legalName: string;
  tradeName: string;
  ruc: string;
  mainAddress: string;
  phone: string;
  email?: string | null;
  requiresAccounting: boolean;
  specialTaxpayer?: string | null;
  logoBase64?: string | null;
  footerText?: string | null;
  receiptWidth: number;
};

export const billingSettingsService = {
  get: () =>
    api
      .get<ApiResponse<BillingSettingsDto | null>>('/api/configuracion-facturacion')
      .then((r) => r.data.responseObject ?? null),

  upsert: (body: UpsertBillingSettingsRequest) =>
    api
      .put<ApiResponse<BillingSettingsDto>>('/api/configuracion-facturacion', body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),
};
