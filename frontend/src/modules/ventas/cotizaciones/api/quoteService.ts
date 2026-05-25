import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export type QuoteStatus =
  | 'Draft'
  | 'Sent'
  | 'Approved'
  | 'Rejected'
  | 'Expired'
  | 'Cancelled'
  | 'Converted';

export interface QuoteLine {
  lineNo: number;
  productId: string;
  productName: string;
  sku: string;
  quantity: number;
  unitPrice: number;
  taxRatePct: number;
  lineTotal: number;
}

export interface QuoteListItem {
  publicId: string;
  quoteNumber: string;
  businessPartnerId: string;
  businessPartnerName: string;
  issueDate: string;
  validUntil: string;
  status: QuoteStatus;
  total: number;
  createdAt: string;
}

export interface QuoteDetail extends QuoteListItem {
  currencyCode: string;
  subtotal: number;
  taxTotal: number;
  paymentTermDays: number;
  notes: string | null;
  lines: QuoteLine[];
  relatedSalesOrderPublicId?: string | null;
}

export interface QuotesPagedResult {
  items: QuoteListItem[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface QuotesFilter {
  pageNumber?: number;
  pageSize?: number;
  businessPartnerId?: string;
  status?: QuoteStatus | '';
  dateFrom?: string;
  dateTo?: string;
}

export interface QuoteLineRequest {
  productId: string;
  quantity: number;
  unitPrice: number;
  taxRatePct: number;
}

export interface CreateQuoteRequest {
  businessPartnerId: string;
  validUntil: string;
  paymentTermDays: number;
  notes: string | null;
  lines: QuoteLineRequest[];
}

export interface ConvertQuoteToOrderRequest {
  quotePublicId: string;
  warehouseId: string;
  requiredDate: string | null;
}

export interface SalesOrderCreated {
  publicId: string;
  orderNumber: string;
}

export const quoteService = {
  async list(filter: QuotesFilter = {}): Promise<QuotesPagedResult> {
    const q = new URLSearchParams();
    if (filter.pageNumber) q.set('pageNumber', String(filter.pageNumber));
    if (filter.pageSize) q.set('pageSize', String(filter.pageSize));
    if (filter.businessPartnerId) q.set('businessPartnerId', filter.businessPartnerId);
    if (filter.status) q.set('status', filter.status);
    if (filter.dateFrom) q.set('dateFrom', filter.dateFrom);
    if (filter.dateTo) q.set('dateTo', filter.dateTo);

    const res = await api.get<ApiResponse<QuotesPagedResult>>(
      `/api/sales/quotes?${q.toString()}`
    );
    return res.data.responseObject ?? { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };
  },

  async getByPublicId(publicId: string): Promise<QuoteDetail | null> {
    const res = await api.get<ApiResponse<QuoteDetail | null>>(
      `/api/sales/quotes/${publicId}`
    );
    return res.data.responseObject ?? null;
  },

  async create(payload: CreateQuoteRequest): Promise<QuoteDetail> {
    const res = await api.post<ApiResponse<QuoteDetail>>('/api/sales/quotes', payload);
    return res.data.responseObject!;
  },

  async approve(publicId: string): Promise<QuoteDetail> {
    const res = await api.post<ApiResponse<QuoteDetail>>(
      `/api/sales/quotes/${publicId}/approve`
    );
    return res.data.responseObject!;
  },

  async convertToOrder(payload: ConvertQuoteToOrderRequest): Promise<SalesOrderCreated> {
    const res = await api.post<ApiResponse<SalesOrderCreated>>('/api/sales/orders/from-quote', payload);
    return res.data.responseObject!;
  },

  async cancel(publicId: string, reason?: string | null): Promise<QuoteDetail> {
    const res = await api.post<ApiResponse<QuoteDetail>>(
      `/api/sales/quotes/${publicId}/cancel`,
      { reason: reason ?? null }
    );
    return res.data.responseObject!;
  },
};
