import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export type SalesOrderStatus =
  | 'Draft'
  | 'Confirmed'
  | 'PartiallyInvoiced'
  | 'Invoiced'
  | 'Cancelled';

export interface SalesOrderLine {
  lineNo: number;
  productId: string;
  productName: string;
  sku: string;
  quantity: number;
  unitPrice: number;
  taxRatePct: number;
  lineTotal: number;
}

export interface SalesOrderListItem {
  publicId: string;
  orderNumber: string;
  businessPartnerId: string;
  businessPartnerName: string;
  issueDate: string;
  requiredDate: string | null;
  status: SalesOrderStatus;
  total: number;
  createdAt: string;
}

export interface SalesOrderDetail extends SalesOrderListItem {
  warehouseId: string;
  warehouseName: string;
  currencyCode: string;
  subtotal: number;
  taxTotal: number;
  paymentTermDays: number;
  notes: string | null;
  lines: SalesOrderLine[];
  relatedInvoicePublicId?: string | null;
}

export interface SalesOrdersPagedResult {
  items: SalesOrderListItem[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface SalesOrdersFilter {
  pageNumber?: number;
  pageSize?: number;
  businessPartnerId?: string;
  status?: SalesOrderStatus | '';
  dateFrom?: string;
  dateTo?: string;
}

export interface SalesOrderLineRequest {
  productId: string;
  quantity: number;
  unitPrice: number;
  taxRatePct: number;
}

export interface CreateSalesOrderRequest {
  businessPartnerId: string;
  warehouseId: string;
  requiredDate: string | null;
  paymentTermDays: number;
  notes: string | null;
  lines: SalesOrderLineRequest[];
}

function readPublicId(payload: unknown): string {
  if (typeof payload === 'string') return payload;
  if (payload && typeof payload === 'object' && 'publicId' in payload) {
    return String((payload as { publicId: string }).publicId);
  }
  return '';
}

export const salesOrderService = {
  async list(filter: SalesOrdersFilter = {}): Promise<SalesOrdersPagedResult> {
    const q = new URLSearchParams();
    if (filter.pageNumber) q.set('pageNumber', String(filter.pageNumber));
    if (filter.pageSize) q.set('pageSize', String(filter.pageSize));
    if (filter.businessPartnerId) q.set('businessPartnerId', filter.businessPartnerId);
    if (filter.status) q.set('status', filter.status);
    if (filter.dateFrom) q.set('dateFrom', filter.dateFrom);
    if (filter.dateTo) q.set('dateTo', filter.dateTo);

    const res = await api.get<ApiResponse<SalesOrdersPagedResult>>(
      `/api/sales/orders?${q.toString()}`
    );
    return res.data.responseObject ?? { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };
  },

  async getByPublicId(publicId: string): Promise<SalesOrderDetail | null> {
    const res = await api.get<ApiResponse<SalesOrderDetail | null>>(
      `/api/sales/orders/${publicId}`
    );
    return res.data.responseObject ?? null;
  },

  async create(payload: CreateSalesOrderRequest): Promise<SalesOrderDetail> {
    const res = await api.post<ApiResponse<SalesOrderDetail>>('/api/sales/orders', payload);
    return res.data.responseObject!;
  },

  async confirm(publicId: string): Promise<SalesOrderDetail> {
    const res = await api.post<ApiResponse<SalesOrderDetail>>(
      `/api/sales/orders/${publicId}/confirm`
    );
    return res.data.responseObject!;
  },

  async createInvoice(publicId: string): Promise<string> {
    const res = await api.post<ApiResponse<string>>(
      `/api/sales/orders/${publicId}/invoice`,
      {}
    );
    return readPublicId(res.data.responseObject);
  },

  async cancel(publicId: string, reason?: string | null): Promise<SalesOrderDetail> {
    const res = await api.post<ApiResponse<SalesOrderDetail>>(
      `/api/sales/orders/${publicId}/cancel`,
      { reason: reason ?? null }
    );
    return res.data.responseObject!;
  },
};
