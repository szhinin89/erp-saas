import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';

export interface VentasFacturaDto {
  id: string;
  clienteId: string;
  clienteNombre: string;
  bodegaId: string;
  sucursalId: string;
  establecimiento: string;
  puntoEmision: string;
  secuencial: string;
  claveAcceso: string;
  fechaEmision: string;
  subtotal: number;
  impuesto: number;
  total: number;
  estado: string;
  numeroAutorizacion: string | null;
  fechaAutorizacion: string | null;
  mensajeError: string | null;
  asientoContableId: string | null;
  createdAt: string;
}

export interface VentasPagedResult {
  items: VentasFacturaDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface CreateSaleItemDto {
  productId: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateSaleRequest {
  customerId: string;
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
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('responseObject' in o && typeof o.responseObject === 'string') return o.responseObject;
    if ('ResponseObject' in o && typeof o.ResponseObject === 'string') return o.ResponseObject;
  }
  return body as string;
}

export const ventasFacturasService = {
  async list(params: { pageNumber?: number; pageSize?: number } = {}): Promise<VentasPagedResult> {
    const pageNumber = params.pageNumber ?? 1;
    const pageSize = params.pageSize ?? 50;
    const q = new URLSearchParams();
    q.set('pageNumber', String(pageNumber));
    q.set('pageSize', String(pageSize));
    const res = await api.get<ApiResponse<VentasPagedResult>>(`/api/sales/invoices?${q.toString()}`);
    return res.data.responseObject ?? { items: [], totalCount: 0, pageNumber, pageSize };
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
      `/api/sales/invoices/stock?productoId=${productId}&bodegaId=${warehouseId}`
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
