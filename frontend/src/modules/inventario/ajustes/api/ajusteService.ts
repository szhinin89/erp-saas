import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

// ── Tipos ──────────────────────────────────────────────────────────────────

export type EstadoAjuste = 'Borrador' | 'Ejecutado' | 'Cancelado';
export type TipoAjuste   = 'Incremento' | 'Disminucion';

export interface AjusteInventario {
  id: string;
  numeroAjuste: string;
  bodegaId: string;
  bodegaNombre: string;
  productoId: string;
  productoNombre: string;
  cantidadAjuste: number;
  tipoAjuste: TipoAjuste;
  motivo: string;
  observaciones: string | null;
  fechaAjuste: string;
  estado: EstadoAjuste;
  fechaEjecucion: string | null;
  ejecutadoPor: string | null;
  createdAt: string;
}

export interface AjustesPagedResult {
  items: AjusteInventario[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface AjustesFilter {
  pageNumber?: number;
  pageSize?: number;
  bodegaId?: string;
  productoId?: string;
  estado?: EstadoAjuste | '';
  fechaDesde?: string;
  fechaHasta?: string;
}

export interface CrearAjusteRequest {
  bodegaId: string;
  productoId: string;
  cantidadAjuste: number;
  motivo: string;
  observaciones: string | null;
}

// ── Servicio ──────────────────────────────────────────────────────────────

export const ajusteService = {
  async getAll(filter: AjustesFilter = {}): Promise<AjustesPagedResult> {
    const q = new URLSearchParams();
    if (filter.pageNumber) q.set('pageNumber', String(filter.pageNumber));
    if (filter.pageSize)   q.set('pageSize',   String(filter.pageSize));
    if (filter.bodegaId)   q.set('bodegaId',   filter.bodegaId);
    if (filter.productoId) q.set('productoId', filter.productoId);
    if (filter.estado)     q.set('estado',     filter.estado);
    if (filter.fechaDesde) q.set('fechaDesde', filter.fechaDesde);
    if (filter.fechaHasta) q.set('fechaHasta', filter.fechaHasta);

    const res = await api.get<ApiResponse<AjustesPagedResult>>(
      `/api/inventario/ajustes?${q.toString()}`
    );
    return res.data.responseObject ?? { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };
  },

  async getById(id: string): Promise<AjusteInventario | null> {
    const res = await api.get<ApiResponse<AjusteInventario | null>>(
      `/api/inventario/ajustes/${id}`
    );
    return res.data.responseObject ?? null;
  },

  async crear(payload: CrearAjusteRequest): Promise<AjusteInventario> {
    const res = await api.post<ApiResponse<AjusteInventario>>(
      '/api/inventario/ajustes',
      payload
    );
    return res.data.responseObject;
  },

  async ejecutar(id: string): Promise<AjusteInventario> {
    const res = await api.patch<ApiResponse<AjusteInventario>>(
      `/api/inventario/ajustes/${id}/ejecutar`
    );
    return res.data.responseObject;
  },

  async cancelar(id: string): Promise<AjusteInventario> {
    const res = await api.patch<ApiResponse<AjusteInventario>>(
      `/api/inventario/ajustes/${id}/cancelar`
    );
    return res.data.responseObject;
  },

  // ── Auxiliar: stock actual para mostrar máximo disponible ───────────────
  async getStockDisponible(bodegaId: string, productoId: string): Promise<number> {
    const q = new URLSearchParams({ bodegaId, productoId });
    const res = await api.get<ApiResponse<Array<{ productoId: string; cantidadDisponible: number }>>>(
      `/api/inventario/stock-actual?${q.toString()}`
    );
    return res.data.responseObject?.[0]?.cantidadDisponible ?? 0;
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
