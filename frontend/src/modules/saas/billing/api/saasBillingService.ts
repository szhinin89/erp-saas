import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export type SubscriberBillingAccountDto = {
  id: string;
  subscriberId: string;
  status: string;
  renewalState: string;
  trialState: string;
  billingEmail: string;
  countryCode: string;
  currencyCode: string;
};

export type SaasBillingInvoiceDto = {
  id: string;
  invoiceNumber: string;
  status: string;
  totalAmount: number;
  currencyCode: string;
  issuedAtUtc: string;
};

export const saasBillingService = {
  async getAccount(): Promise<SubscriberBillingAccountDto | null> {
    const { data } = await api.get<ApiResponse<SubscriberBillingAccountDto>>('/api/saas/billing/account');
    return data.responseObject ?? null;
  },

  async listInvoices(take = 10): Promise<SaasBillingInvoiceDto[]> {
    const { data } = await api.get<ApiResponse<SaasBillingInvoiceDto[]>>('/api/saas/billing/invoices', {
      params: { take },
    });
    return data.responseObject ?? [];
  },
};
