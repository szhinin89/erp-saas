import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

// ── Tipos de dominio ────────────────────────────────────────────────────────

export type EstadoTransferencia = 'Borrador' | 'Confirmado' | 'Cancelado';

export interface TransferenciaDetalle {
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
  status: EstadoTransferencia;
  reason: string | null;
  notes: string | null;
  confirmationDate: string | null;
  confirmedBy: string | null;
  createdAt: string;
}

export interface TransferenciaDetail extends Transferencia {
  detalles: TransferenciaDetalle[];
}

export interface TransferenciasPagedResult {
  items: Transferencia[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface ItemTransferenciaRequest {
  productId: string;
  quantity: number;
}

export interface CrearTransferenciaRequest {
  sourceWarehouseId: string;
  targetWarehouseId: string;
  reason: string | null;
  notes: string | null;
  items: ItemTransferenciaRequest[];
}

export interface TransferenciasFilter {
  pageNumber?: number;
  pageSize?: number;
  bodegaOrigenId?: string;
  bodegaDestinoId?: string;
  estado?: EstadoTransferencia | '';
  fechaDesde?: string;
  fechaHasta?: string;
}

// ── Tipos auxiliares (Bodegas y Stock) ────────────────────────────────────────

export interface BodegaOpcion {
  id: string;
  name: string;
  isActive: boolean;
}

export interface StockDisponible {
  productId: string;
  availableQuantity: number;
}

// ── Servicio ───────────────────────────────────────────────────────────────────

export const transferenciaService = {
  async getAll(filter: TransferenciasFilter = {}): Promise<TransferenciasPagedResult> {
    const q = new URLSearchParams();
    if (filter.pageNumber) q.set('pageNumber', String(filter.pageNumber));
    if (filter.pageSize)   q.set('pageSize',   String(filter.pageSize));
    if (filter.bodegaOrigenId)  q.set('bodegaOrigenId',  filter.bodegaOrigenId);
    if (filter.bodegaDestinoId) q.set('bodegaDestinoId', filter.bodegaDestinoId);
    if (filter.estado)     q.set('estado',     filter.estado);
    if (filter.fechaDesde) q.set('fechaDesde', filter.fechaDesde);
    if (filter.fechaHasta) q.set('fechaHasta', filter.fechaHasta);

    const res = await api.get<ApiResponse<TransferenciasPagedResult>>(
      `/api/inventario/transferencias?${q.toString()}`
    );
    return res.data.responseObject ?? { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };
  },

  async getById(id: string): Promise<TransferenciaDetail | null> {
    const res = await api.get<ApiResponse<TransferenciaDetail | null>>(
      `/api/inventario/transferencias/${id}`
    );
    return res.data.responseObject ?? null;
  },

  async crear(payload: CrearTransferenciaRequest): Promise<Transferencia> {
    const res = await api.post<ApiResponse<Transferencia>>(
      '/api/inventario/transferencias',
      payload
    );
    return res.data.responseObject;
  },

  async confirmar(id: string): Promise<Transferencia> {
    const res = await api.patch<ApiResponse<Transferencia>>(
      `/api/inventario/transferencias/${id}/confirmar`
    );
    return res.data.responseObject;
  },

  async cancelar(id: string): Promise<Transferencia> {
    const res = await api.patch<ApiResponse<Transferencia>>(
      `/api/inventario/transferencias/${id}/cancelar`
    );
    return res.data.responseObject;
  },

  // ── Auxiliares ─────────────────────────────────────────────────────────────

  async getBodegas(): Promise<BodegaOpcion[]> {
    const res = await api.get<ApiResponse<BodegaOpcion[]>>('/api/bodegas');
    return (res.data.responseObject ?? []).filter((b) => b.isActive);
  },

  async getStockDisponible(bodegaId: string, productoId: string): Promise<number> {
    const q = new URLSearchParams({ bodegaId, productoId });
    const res = await api.get<ApiResponse<StockDisponible[]>>(
      `/api/inventario/stock-actual?${q.toString()}`
    );
    return res.data.responseObject?.[0]?.availableQuantity ?? 0;
  },
};
