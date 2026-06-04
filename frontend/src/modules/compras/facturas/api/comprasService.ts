import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

// Estado enum: 0=Draft, 1=Validated, 2=Approved, 3=Rejected
export type EstadoCompra = 0 | 1 | 2 | 3;

export interface CompraLineaDto {
  id: string;
  productId: string | null;
  description: string;
  supplierPrimaryCode: string | null;
  quantity: number;
  unitPrice: number;
  discountPercentage: number;
  subtotal: number;
  vatPercentage: number;
  vatAmount: number;
  total: number;
}

export interface CompraDto {
  id: string;
  supplierId: string;
  businessPartnerId?: string | null;
  invoiceNumber: string;
  accessKey: string | null;
  xmlPath: string | null;
  invoiceDate: string;
  dueDate: string | null;
  estado: EstadoCompra;
  paymentTerms: string;
  subtotal: number;
  vatTotal: number;
  total: number;
  notes: string | null;
  journalEntryId: string | null;
  createdAt: string;
}

export interface CompraDetalleDto extends CompraDto {
  validatedBy: string | null;
  validatedAt: string | null;
  approvedBy: string | null;
  approvedAt: string | null;
  rejectedBy: string | null;
  rejectedAt: string | null;
  rejectionReason: string | null;
  lines: CompraLineaDto[];
}

export interface CompraLineaInput {
  description: string;
  productCode: string | null;
  productId: string | null;
  quantity: number;
  unitPrice: number;
  discountPct: number;
  vatPct: number;
}

export interface CrearCompraManualRequest {
  /** V2: businessPartnerId es el ID canónico del proveedor. supplierId eliminado. */
  businessPartnerId: string;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate: string | null;
  paymentTerms: string | null;
  notes: string | null;
  lines: CompraLineaInput[];
}

function readObj<T>(body: unknown): T {
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('responseObject' in o) return o.responseObject as T;
    if ('ResponseObject' in o) return o.ResponseObject as T;
  }
  return body as T;
}

export const comprasService = {
  async list(filtros: { estado?: EstadoCompra; proveedorId?: string } = {}): Promise<CompraDto[]> {
    const q = new URLSearchParams();
    if (filtros.estado !== undefined) q.set('estado', String(filtros.estado));
    if (filtros.proveedorId) q.set('proveedorId', filtros.proveedorId);
    const res = await api.get<ApiResponse<CompraDto[]>>(`/api/purchases/invoices?${q.toString()}`);
    return readObj<CompraDto[]>(res.data) ?? [];
  },

  async getById(id: string): Promise<CompraDetalleDto | null> {
    const res = await api.get<ApiResponse<CompraDetalleDto>>(`/api/purchases/invoices/${id}`);
    return readObj<CompraDetalleDto | null>(res.data);
  },

  async crearManual(payload: CrearCompraManualRequest): Promise<CompraDto> {
    const res = await api.post<ApiResponse<CompraDto>>('/api/purchases/invoices/manual', {
      modo: 2, // Manual
      businessPartnerId: payload.businessPartnerId,
      invoiceNumber: payload.invoiceNumber,
      invoiceDate: payload.invoiceDate,
      dueDate: payload.dueDate,
      paymentTerms: payload.paymentTerms ?? '',
      notes: payload.notes,
      lines: payload.lines,
      xmlContent: null,
      xmlFileName: null,
      warehouseAllocations: null,
    });
    return readObj<CompraDto>(res.data);
  },

  async validar(id: string): Promise<void> {
    await api.patch(`/api/purchases/invoices/${id}/validar`, {});
  },

  async aprobar(id: string): Promise<void> {
    await api.patch(`/api/purchases/invoices/${id}/aprobar`, {});
  },

  async rechazar(id: string, motivo: string): Promise<void> {
    await api.patch(`/api/purchases/invoices/${id}/rechazar`, { reason: motivo });
  },
};
