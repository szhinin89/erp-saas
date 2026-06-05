import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

// ── Tipos de dominio ────────────────────────────────────────────────────────

export type TransferStatus = 'Borrador' | 'Confirmado' | 'Cancelado';

export interface TransferLine {
  id: string;
  productId: string;
  description: string;
  quantity: number;
}

export interface Transferencia {
  id: string;
  transferNumber: string;
  sourceWarehouseId: string;
  sourceWarehouseName: string;
  destinationWarehouseId: string;
  destinationWarehouseName: string;
  transferDate: string;
  status: TransferStatus;
  reason: string | null;
  notes: string | null;
  confirmationDate: string | null;
  confirmedBy: string | null;
  createdAt: string;
}

export interface TransferDetail extends Transferencia {
  detalles: TransferLine[];
}

export interface TransfersPagedResult {
  items: Transferencia[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface TransferItemRequest {
  productId: string;
  quantity: number;
}

export interface CreateTransferRequest {
  sourceWarehouseId: string;
  targetWarehouseId: string;
  reason: string | null;
  notes: string | null;
  items: TransferItemRequest[];
}

export interface TransfersFilter {
  pageNumber?: number;
  pageSize?: number;
  sourceWarehouseId?: string;
  targetWarehouseId?: string;
  status?: TransferStatus | '';
  dateFrom?: string;
  dateTo?: string;
}

// ── Tipos auxiliares (Bodegas y Stock) ────────────────────────────────────────

export interface WarehouseOption {
  id: string;
  name: string;
  isActive: boolean;
}

export interface AvailableStock {
  productId: string;
  availableQuantity: number;
}

// ── Servicio ───────────────────────────────────────────────────────────────────

export const transferService = {
  async getAll(filter: TransfersFilter = {}): Promise<TransfersPagedResult> {
    const q = new URLSearchParams();
    if (filter.pageNumber) q.set('pageNumber', String(filter.pageNumber));
    if (filter.pageSize)   q.set('pageSize',   String(filter.pageSize));
    if (filter.sourceWarehouseId)  q.set('sourceWarehouseId',  filter.sourceWarehouseId);
    if (filter.targetWarehouseId) q.set('targetWarehouseId', filter.targetWarehouseId);
    if (filter.status)     q.set('estado',     filter.status);
    if (filter.dateFrom) q.set('dateFrom', filter.dateFrom);
    if (filter.dateTo) q.set('dateTo', filter.dateTo);

    const res = await api.get<ApiResponse<TransfersPagedResult>>(
      `/api/inventory/transfers?${q.toString()}`
    );
    return res.data.responseObject ?? { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };
  },

  async getById(id: string): Promise<TransferDetail | null> {
    const res = await api.get<ApiResponse<TransferDetail | null>>(
      `/api/inventory/transfers/${id}`
    );
    return res.data.responseObject ?? null;
  },

  async crear(payload: CreateTransferRequest): Promise<Transferencia> {
    const res = await api.post<ApiResponse<Transferencia>>(
      '/api/inventory/transfers',
      payload
    );
    return res.data.responseObject;
  },

  async confirmar(id: string): Promise<Transferencia> {
    const res = await api.patch<ApiResponse<Transferencia>>(
      `/api/inventory/transfers/${id}/confirmar`
    );
    return res.data.responseObject;
  },

  async cancelar(id: string): Promise<Transferencia> {
    const res = await api.patch<ApiResponse<Transferencia>>(
      `/api/inventory/transfers/${id}/cancelar`
    );
    return res.data.responseObject;
  },

  // ── Auxiliares ─────────────────────────────────────────────────────────────

  async getBodegas(): Promise<WarehouseOption[]> {
    const res = await api.get<ApiResponse<WarehouseOption[]>>('/api/inventory/warehouses');
    return (res.data.responseObject ?? []).filter((b) => b.isActive);
  },

  async getStockDisponible(warehouseId: string, productId: string): Promise<number> {
    const q = new URLSearchParams({ warehouseId, productId });
    const res = await api.get<ApiResponse<AvailableStock[]>>(
      `/api/inventory/stock/stock-actual?${q.toString()}`
    );
    return res.data.responseObject?.[0]?.availableQuantity ?? 0;
  },
};
