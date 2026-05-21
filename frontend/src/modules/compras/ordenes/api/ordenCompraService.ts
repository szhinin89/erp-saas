import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

// ── Tipos ──────────────────────────────────────────────────────────────────

export type EstadoOrdenCompra =
  | 'Borrador'
  | 'Enviada'
  | 'Aprobada'
  | 'RecibidaParcial'
  | 'Cerrada'
  | 'Cancelada';

export interface OrdenCompraDetalle {
  id: string;
  productoId: string;
  descripcion: string;
  cantidadPedida: number;
  cantidadFacturada: number;
  pendienteFacturar: number;
  precioUnitario: number;
  subtotal: number;
  impuesto: number;
  total: number;
}

export interface OrdenCompraFacturaVinculada {
  compraFacturaId: string;
  numeroFactura: string;
  fechaVinculacion: string;
}

export interface OrdenCompra {
  id: string;
  numeroOrden: string;
  proveedorId: string;
  proveedorNombre: string;
  fechaEmision: string;
  fechaRequerida: string;
  estado: EstadoOrdenCompra;
  moneda: string;
  subtotal: number;
  impuesto: number;
  total: number;
  observaciones: string | null;
  createdAt: string;
}

export interface OrdenCompraDetail extends OrdenCompra {
  direccionEntrega: string | null;
  bodegaDestinoId: string | null;
  fechaEnvio: string | null;
  fechaAprobacion: string | null;
  aprobadoPor: string | null;
  fechaCierre: string | null;
  detalles: OrdenCompraDetalle[];
  facturasVinculadas: OrdenCompraFacturaVinculada[];
}

export interface OrdenesCompraPagedResult {
  items: OrdenCompra[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface OrdenesCompraFilter {
  pageNumber?: number;
  pageSize?: number;
  proveedorId?: string;
  estado?: EstadoOrdenCompra | '';
  fechaDesde?: string;
  fechaHasta?: string;
}

export interface ItemOrdenCompraRequest {
  productoId: string;
  cantidad: number;
  precioUnitario: number;
  ivaPorcentaje?: number;
}

export interface CrearOrdenCompraRequest {
  proveedorId: string;
  fechaRequerida: string;
  bodegaDestinoId: string | null;
  direccionEntrega: string | null;
  observaciones: string | null;
  items: ItemOrdenCompraRequest[];
}

// ── Servicio ──────────────────────────────────────────────────────────────

export const ordenCompraService = {
  async getAll(filter: OrdenesCompraFilter = {}): Promise<OrdenesCompraPagedResult> {
    const q = new URLSearchParams();
    if (filter.pageNumber)  q.set('pageNumber',  String(filter.pageNumber));
    if (filter.pageSize)    q.set('pageSize',    String(filter.pageSize));
    if (filter.proveedorId) q.set('proveedorId', filter.proveedorId);
    if (filter.estado)      q.set('estado',      filter.estado);
    if (filter.fechaDesde)  q.set('fechaDesde',  filter.fechaDesde);
    if (filter.fechaHasta)  q.set('fechaHasta',  filter.fechaHasta);

    const res = await api.get<ApiResponse<OrdenesCompraPagedResult>>(
      `/api/purchases/orders?${q.toString()}`
    );
    return res.data.responseObject ?? { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };
  },

  async getById(id: string): Promise<OrdenCompraDetail | null> {
    const res = await api.get<ApiResponse<OrdenCompraDetail | null>>(
      `/api/purchases/orders/${id}`
    );
    return res.data.responseObject ?? null;
  },

  async getPendientesPorFacturar(): Promise<OrdenCompra[]> {
    const res = await api.get<ApiResponse<OrdenCompra[]>>(
      '/api/purchases/orders/pendientes-por-facturar'
    );
    return res.data.responseObject ?? [];
  },

  async crear(payload: CrearOrdenCompraRequest): Promise<OrdenCompra> {
    const res = await api.post<ApiResponse<OrdenCompra>>(
      '/api/purchases/orders',
      payload
    );
    return res.data.responseObject;
  },

  async enviar(id: string): Promise<OrdenCompra> {
    const res = await api.patch<ApiResponse<OrdenCompra>>(
      `/api/purchases/orders/${id}/enviar`
    );
    return res.data.responseObject;
  },

  async aprobar(id: string): Promise<OrdenCompra> {
    const res = await api.patch<ApiResponse<OrdenCompra>>(
      `/api/purchases/orders/${id}/aprobar`
    );
    return res.data.responseObject;
  },

  async cancelar(id: string): Promise<OrdenCompra> {
    const res = await api.patch<ApiResponse<OrdenCompra>>(
      `/api/purchases/orders/${id}/cancelar`
    );
    return res.data.responseObject;
  },

  async vincularFactura(id: string, compraFacturaId: string): Promise<OrdenCompra> {
    const res = await api.post<ApiResponse<OrdenCompra>>(
      `/api/purchases/orders/${id}/vincular-factura`,
      { compraFacturaId }
    );
    return res.data.responseObject;
  },
};
