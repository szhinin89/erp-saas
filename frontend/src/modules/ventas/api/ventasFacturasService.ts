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

export const ventasFacturasService = {
  async list(params: { pageNumber?: number; pageSize?: number } = {}): Promise<VentasPagedResult> {
    const pageNumber = params.pageNumber ?? 1;
    const pageSize = params.pageSize ?? 50;
    const q = new URLSearchParams();
    q.set('pageNumber', String(pageNumber));
    q.set('pageSize', String(pageSize));

    const res = await api.get<ApiResponse<VentasPagedResult>>(`/api/ventas?${q.toString()}`);
    return (
      res.data.responseObject ?? {
        items: [],
        totalCount: 0,
        pageNumber,
        pageSize,
      }
    );
  },
};
