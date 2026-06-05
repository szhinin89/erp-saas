import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

// ── Tipos ──────────────────────────────────────────────────────────────────

export type AdjustmentStatus = 'Borrador' | 'Ejecutado' | 'Cancelado';
export type AdjustmentTypeEnum   = 'Incremento' | 'Disminucion';

export interface InventoryAdjustment {
  id: string;
  adjustmentNumber: string;
  warehouseId: string;
  warehouseName: string;
  productId: string;
  productName: string;
  adjustmentQuantity: number;
  adjustmentType: AdjustmentTypeEnum;
  reason: string;
  notes: string | null;
  adjustmentDate: string;
  status: AdjustmentStatus;
  executionDate: string | null;
  executedBy: string | null;
  createdAt: string;
}

export interface AdjustmentsPagedResult {
  items: InventoryAdjustment[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface AdjustmentsFilter {
  pageNumber?: number;
  pageSize?: number;
  warehouseId?: string;
  productId?: string;
  status?: AdjustmentStatus | '';
  dateFrom?: string;
  dateTo?: string;
}

export interface CreateAdjustmentRequest {
  warehouseId: string;
  productId: string;
  adjustmentQty: number;
  reason: string;
  notes: string | null;
}

// ── Servicio ──────────────────────────────────────────────────────────────

export const adjustmentService = {
  async getAll(filter: AdjustmentsFilter = {}): Promise<AdjustmentsPagedResult> {
    const q = new URLSearchParams();
    if (filter.pageNumber) q.set('pageNumber', String(filter.pageNumber));
    if (filter.pageSize)   q.set('pageSize',   String(filter.pageSize));
    if (filter.warehouseId)   q.set('warehouseId',   filter.warehouseId);
    if (filter.productId) q.set('productId', filter.productId);
    if (filter.status)     q.set('estado',     filter.status);
    if (filter.dateFrom) q.set('dateFrom', filter.dateFrom);
    if (filter.dateTo) q.set('dateTo', filter.dateTo);

    const res = await api.get<ApiResponse<AdjustmentsPagedResult>>(
      `/api/inventory/adjustments?${q.toString()}`
    );
    return res.data.responseObject ?? { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };
  },

  async getById(id: string): Promise<InventoryAdjustment | null> {
    const res = await api.get<ApiResponse<InventoryAdjustment | null>>(
      `/api/inventory/adjustments/${id}`
    );
    return res.data.responseObject ?? null;
  },

  async crear(payload: CreateAdjustmentRequest): Promise<InventoryAdjustment> {
    const res = await api.post<ApiResponse<InventoryAdjustment>>(
      '/api/inventory/adjustments',
      payload
    );
    return res.data.responseObject;
  },

  async ejecutar(id: string): Promise<InventoryAdjustment> {
    const res = await api.patch<ApiResponse<InventoryAdjustment>>(
      `/api/inventory/adjustments/${id}/ejecutar`
    );
    return res.data.responseObject;
  },

  async cancelar(id: string): Promise<InventoryAdjustment> {
    const res = await api.patch<ApiResponse<InventoryAdjustment>>(
      `/api/inventory/adjustments/${id}/cancelar`
    );
    return res.data.responseObject;
  },

  // ── Auxiliar: stock actual para mostrar máximo disponible ───────────────
  async getStockDisponible(warehouseId: string, productId: string): Promise<number> {
    const q = new URLSearchParams({ warehouseId, productId });
    const res = await api.get<ApiResponse<Array<{ productId: string; availableQuantity: number }>>>(
      `/api/inventory/stock/stock-actual?${q.toString()}`
    );
    return res.data.responseObject?.[0]?.availableQuantity ?? 0;
  },
};

export const MOTIVOS_PREDEFINIDOS = [
  'Ajuste físico',
  'Merma',
  'Sobrante',
  'Robo',
  'Error de inventario',
  'Donación',
  'Vencimiento',
  'Otro',
] as const;
