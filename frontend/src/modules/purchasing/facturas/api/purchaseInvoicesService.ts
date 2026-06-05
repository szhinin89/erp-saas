import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

// Estado enum: 0=Draft, 1=Validated, 2=Approved, 3=Rejected
export type PurchaseInvoiceStatus = 0 | 1 | 2 | 3;

export interface PurchaseInvoiceLineDto {
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

export interface PurchaseInvoiceDto {
  id: string;
  supplierId: string;
  businessPartnerId?: string | null;
  invoiceNumber: string;
  accessKey: string | null;
  xmlPath: string | null;
  invoiceDate: string;
  dueDate: string | null;
  status: PurchaseInvoiceStatus;
  paymentTerms: string;
  subtotal: number;
  vatTotal: number;
  total: number;
  notes: string | null;
  journalEntryId: string | null;
  createdAt: string;
}

export interface PurchaseInvoiceDetailDto extends PurchaseInvoiceDto {
  validatedBy: string | null;
  validatedAt: string | null;
  approvedBy: string | null;
  approvedAt: string | null;
  rejectedBy: string | null;
  rejectedAt: string | null;
  rejectionReason: string | null;
  lines: PurchaseInvoiceLineDto[];
}

export interface PurchaseInvoiceLineInput {
  description: string;
  productCode: string | null;
  productId: string | null;
  quantity: number;
  unitPrice: number;
  discountPct: number;
  vatPct: number;
}

export interface CreatePurchaseInvoiceRequest {
  /** V2: businessPartnerId es el ID canónico del proveedor. supplierId eliminado. */
  businessPartnerId: string;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate: string | null;
  paymentTerms: string | null;
  notes: string | null;
  lines: PurchaseInvoiceLineInput[];
}

function readObj<T>(body: unknown): T {
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('responseObject' in o) return o.responseObject as T;
    if ('ResponseObject' in o) return o.ResponseObject as T;
  }
  return body as T;
}

export const purchaseInvoicesService = {
  async list(filtros: { estado?: PurchaseInvoiceStatus; proveedorId?: string } = {}): Promise<PurchaseInvoiceDto[]> {
    const q = new URLSearchParams();
    if (filtros.status !== undefined) q.set('estado', String(filtros.status));
    if (filtros.proveedorId) q.set('proveedorId', filtros.proveedorId);
    const res = await api.get<ApiResponse<PurchaseInvoiceDto[]>>(`/api/purchases/invoices?${q.toString()}`);
    return readObj<PurchaseInvoiceDto[]>(res.data) ?? [];
  },

  async getById(id: string): Promise<PurchaseInvoiceDetailDto | null> {
    const res = await api.get<ApiResponse<PurchaseInvoiceDetailDto>>(`/api/purchases/invoices/${id}`);
    return readObj<PurchaseInvoiceDetailDto | null>(res.data);
  },

  async crearManual(payload: CreatePurchaseInvoiceRequest): Promise<PurchaseInvoiceDto> {
    const res = await api.post<ApiResponse<PurchaseInvoiceDto>>('/api/purchases/invoices/manual', {
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
    return readObj<PurchaseInvoiceDto>(res.data);
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
