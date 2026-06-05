import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';
import {
  mapSalesBillDetailToVentasFactura,
  mapSalesPagedResult,
} from './ventasFacturasMapper';

export interface SalesInvoiceDto {
  id: string;
  clienteId: string;
  clienteNombre: string;
  businessPartnerId?: string | null;
  warehouseId: string;
  sucursalId: string;
  establecimiento: string;
  puntoEmision: string;
  secuencial: string;
  claveAcceso: string;
  issueDate: string;
  subtotal: number;
  vatTotal: number;
  total: number;
  status: string;
  numeroAutorizacion: string | null;
  fechaAutorizacion: string | null;
  mensajeError: string | null;
  asientoContableId: string | null;
  createdAt: string;
}

export interface SalesInvoiceLineDto {
  id: string;
  productId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  subtotal: number;
  vatTotal: number;
  total: number;
}

export interface SalesInvoiceDetailDto extends SalesInvoiceDto {
  docType: string;
  xmlSignedPath: string | null;
  xmlAuthPath: string | null;
  lines: SalesInvoiceLineDto[];
}

export interface SalesPagedResult {
  items: SalesInvoiceDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface SalesInvoicesFilter {
  pageNumber?: number;
  pageSize?: number;
  clienteId?: string;
  desde?: string;
  hasta?: string;
  status?: string;
  search?: string;
}

export interface CreateSaleItemDto {
  productId: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateSaleRequest {
  /** V2: businessPartnerId es el ID canónico del tercero. customerId eliminado. */
  businessPartnerId: string;
  warehouseId: string;
  branchId: string;
  items: CreateSaleItemDto[];
}

export interface StockDisponibleDto {
  productId: string;
  warehouseId: string;
  availableQty: number;
  totalQty: number;
  reservedQty: number;
}

function readId(body: unknown): string {
  if (typeof body === 'string') return body;
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('responseObject' in o && typeof o.responseObject === 'string') return o.responseObject;
    if ('ResponseObject' in o && typeof o.ResponseObject === 'string') return o.ResponseObject;
  }
  return body as string;
}

export const salesInvoicesService = {
  async list(params: SalesInvoicesFilter = {}): Promise<SalesPagedResult> {
    const pageNumber = params.pageNumber ?? 1;
    const pageSize = params.pageSize ?? 50;
    const q = new URLSearchParams();
    q.set('pageNumber', String(pageNumber));
    q.set('pageSize', String(pageSize));
    if (params.clienteId) q.set('clienteId', params.clienteId);
    if (params.desde) q.set('desde', params.desde);
    if (params.hasta) q.set('hasta', params.hasta);
    if (params.status) q.set('estado', params.status);
    if (params.search) q.set('search', params.search);

    const res = await api.get<ApiResponse<{ items: unknown[]; totalCount: number; pageNumber: number; pageSize: number }>>(
      `/api/sales/invoices?${q.toString()}`
    );
    const raw = res.data.responseObject;
    return raw
      ? mapSalesPagedResult(raw as Parameters<typeof mapSalesPagedResult>[0])
      : { items: [], totalCount: 0, pageNumber, pageSize };
  },

  async getById(id: string): Promise<SalesInvoiceDetailDto | null> {
    const res = await api.get<ApiResponse<Record<string, unknown> | null>>(
      `/api/sales/invoices/${id}`
    );
    const raw = res.data.responseObject;
    return raw
      ? mapSalesBillDetailToVentasFactura(
          raw as unknown as Parameters<typeof mapSalesBillDetailToVentasFactura>[0],
        )
      : null;
  },

  async create(payload: CreateSaleRequest): Promise<string> {
    const res = await api.post<ApiResponse<string>>('/api/sales/invoices', payload);
    return readId(res.data);
  },

  async validate(id: string): Promise<string> {
    const res = await api.patch<ApiResponse<string>>(`/api/sales/invoices/${id}/validar`, {});
    return readId(res.data);
  },

  async issue(id: string): Promise<string> {
    const res = await api.patch<ApiResponse<string>>(`/api/sales/invoices/${id}/emitir`, {});
    return readId(res.data);
  },

  async void(id: string): Promise<string> {
    const res = await api.patch<ApiResponse<string>>(`/api/sales/invoices/${id}/anular`, {});
    return readId(res.data);
  },

  async getStock(productId: string, warehouseId: string): Promise<StockDisponibleDto | null> {
    const res = await api.get<ApiResponse<StockDisponibleDto>>(
      `/api/sales/invoices/stock?productId=${productId}&warehouseId=${warehouseId}`
    );
    return res.data.responseObject ?? null;
  },

  async retry(id: string): Promise<void> {
    await api.patch(`/api/sales/invoices/${id}/reintentar`, {});
  },

  async downloadRide(id: string): Promise<Blob> {
    const res = await api.get(`/api/sales/invoices/${id}/ride`, { responseType: 'blob' });
    return res.data as Blob;
  },
};
