import { apiGet, apiPost, apiPut, apiPatch } from '../../lib/apiEnvelope';

export type PaymentTermDto = {
  id: string;
  code: string;
  name: string;
  installments: number;
  daysBetweenInstallments: number;
  totalDays: number;
  summary: string;
  isActive: boolean;
};

export type CreatePaymentTermPayload = {
  code: string;
  name: string;
  installments: number;
  daysBetweenInstallments: number;
};

export type UpdatePaymentTermPayload = {
  id: string;
  name: string;
  installments: number;
  daysBetweenInstallments: number;
};

const BASE = '/api/v1/master/payment-terms';

export const paymentTermService = {
  list: (search?: string) => {
    const params = new URLSearchParams();
    if (search?.trim()) params.set('search', search.trim());
    const qs = params.toString();
    return apiGet<PaymentTermDto[]>(`${BASE}${qs ? `?${qs}` : ''}`);
  },

  getById: (id: string) => apiGet<PaymentTermDto>(`${BASE}/${id}`),

  create: (p: CreatePaymentTermPayload) => apiPost<PaymentTermDto>(BASE, p),

  update: (id: string, p: UpdatePaymentTermPayload) => apiPut<PaymentTermDto>(`${BASE}/${id}`, p),

  enable: (id: string) => apiPatch<boolean>(`${BASE}/${id}/enable`),

  disable: (id: string) => apiPatch<boolean>(`${BASE}/${id}/disable`),
};
