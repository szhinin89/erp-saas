import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export type SubscriberBillingAccountDto = {
  id: string;
  subscriberId: string;
  status: string;          // Trialing | Active | PastDue | GracePeriod | Suspended | Cancelled
  renewalState: string;    // Active | PendingCancellation | Cancelled | Paused
  trialState: string;      // None | Active | Expired
  billingEmail: string;
  countryCode: string;
  currencyCode: string;
  trialEndsAtUtc?: string | null;
  gracePeriodEndsAtUtc?: string | null;
  currentPeriodEndUtc?: string | null;
};

export type SaasBillingInvoiceDto = {
  id: string;
  invoiceNumber: string;
  status: string;          // Draft | Open | Paid | Void | Uncollectible
  currencyCode: string;
  subtotal: number;
  taxAmount: number;
  total: number;
  /** @deprecated use total */
  totalAmount?: number;
  periodStartUtc?: string | null;
  periodEndUtc?: string | null;
  issuedAtUtc?: string | null;
  dueAtUtc?: string | null;
  paidAtUtc?: string | null;
  erpInvoiceId?: string | null;
  lines?: SaasBillingInvoiceLineDto[];
};

export type SaasBillingInvoiceLineDto = {
  description: string;
  lineType: string;
  quantity: number;
  unitAmount: number;
  lineTotal: number;
};

export const saasBillingService = {
  async getAccount(): Promise<SubscriberBillingAccountDto | null> {
    const { data } = await api.get<ApiResponse<SubscriberBillingAccountDto>>('/api/saas/billing/account');
    return data.responseObject ?? null;
  },

  async listInvoices(take = 20): Promise<SaasBillingInvoiceDto[]> {
    const { data } = await api.get<ApiResponse<SaasBillingInvoiceDto[]>>('/api/saas/billing/invoices', {
      params: { take },
    });
    // Normalize: some backends return totalAmount, others total
    return (data.responseObject ?? []).map((inv) => ({
      ...inv,
      total: inv.total ?? inv.totalAmount ?? 0,
    }));
  },
};
