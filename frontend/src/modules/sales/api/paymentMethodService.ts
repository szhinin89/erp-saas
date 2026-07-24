import { apiGet, apiPost, apiPut } from '../../lib/apiEnvelope';

/** Esquema de detalle que la UI debe capturar al registrar un pago — viene del catálogo, nunca se infiere del código. */
export type PaymentMethodDetailType = 'None' | 'Card' | 'Transfer' | 'Check';

export type PaymentMethodDto = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  requiresReference: boolean;
  isCreditAllowed: boolean;
  sortOrder: number;
  detailType: PaymentMethodDetailType;
};

export type CreatePaymentMethodPayload = {
  code: string;
  name: string;
  requiresReference?: boolean;
  isCreditAllowed?: boolean;
  sortOrder?: number;
  detailType?: PaymentMethodDetailType;
};

export type UpdatePaymentMethodPayload = {
  id: string;
  name: string;
  requiresReference: boolean;
  isCreditAllowed: boolean;
  sortOrder: number;
  detailType: PaymentMethodDetailType;
};

const BASE = '/api/v1/payment-methods';

export const paymentMethodService = {
  list: (onlyActive = false) =>
    apiGet<PaymentMethodDto[]>(`${BASE}?onlyActive=${onlyActive}`),

  getById: (id: string) =>
    apiGet<PaymentMethodDto>(`${BASE}/${id}`),

  create: (body: CreatePaymentMethodPayload) =>
    apiPost<PaymentMethodDto>(BASE, body),

  update: (id: string, body: UpdatePaymentMethodPayload) =>
    apiPut<PaymentMethodDto>(`${BASE}/${id}`, body),

  toggle: (id: string) =>
    apiPost<PaymentMethodDto>(`${BASE}/${id}/toggle`, {}),
};
