import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

// ── Tipos ──────────────────────────────────────────────────────────────────

export type PurchaseOrderStatus =
  | 'Borrador'
  | 'Enviada'
  | 'Aprobada'
  | 'RecibidaParcial'
  | 'Cerrada'
  | 'Cancelada';

export interface PurchaseOrderLine {
  id: string;
  productId: string;
  description: string;
  orderedQuantity: number;
  invoicedQuantity: number;
  pendingBillingQuantity: number;
  unitPrice: number;
  subtotal: number;
  vatTotal: number;
  total: number;
}

export interface LinkedPurchaseInvoice {
  purchBillId: string;
  invoiceNumber: string;
  linkedDate: string;
}

export interface PurchaseOrder {
  id: string;
  orderNumber: string;
  /** V2: businessPartnerId es el ID canónico del proveedor (antes: proveedorId). */
  businessPartnerId: string;
  supplierName: string;
  issueDate: string;
  requiredDate: string;
  status: PurchaseOrderStatus;
  currency: string;
  subtotal: number;
  vatTotal: number;
  total: number;
  notes: string | null;
  createdAt: string;
}

export interface PurchaseOrderDetail extends PurchaseOrder {
  deliveryAddress: string | null;
  targetWarehouseId: string | null;
  shippedDate: string | null;
  approvalDate: string | null;
  approvedBy: string | null;
  closingDate: string | null;
  detalles: PurchaseOrderLine[];
  linkedInvoices: LinkedPurchaseInvoice[];
}

export interface PurchaseOrdersPagedResult {
  items: PurchaseOrder[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface PurchaseOrdersFilter {
  pageNumber?: number;
  pageSize?: number;
  /** V2: filtrar por businessPartnerId del proveedor. */
  businessPartnerId?: string;
  status?: PurchaseOrderStatus | '';
  dateFrom?: string;
  dateTo?: string;
}

export interface PurchaseOrderItemRequest {
  productId: string;
  quantity: number;
  unitPrice: number;
  vatPct?: number;
}

export interface CreatePurchaseOrderRequest {
  /** V2: businessPartnerId es el ID canónico del proveedor. Antes: proveedorId. */
  businessPartnerId: string;
  requiredDate: string;
  targetWarehouseId: string | null;
  deliveryAddress: string | null;
  notes: string | null;
  items: PurchaseOrderItemRequest[];
}

// ── Servicio ──────────────────────────────────────────────────────────────

export const purchaseOrderService = {
  async getAll(filter: PurchaseOrdersFilter = {}): Promise<PurchaseOrdersPagedResult> {
    const q = new URLSearchParams();
    if (filter.pageNumber)       q.set('pageNumber',  String(filter.pageNumber));
    if (filter.pageSize)         q.set('pageSize',    String(filter.pageSize));
    if (filter.businessPartnerId) q.set('proveedorId', filter.businessPartnerId);
    if (filter.status)           q.set('estado',      filter.status);
    if (filter.dateFrom)       q.set('dateFrom',  filter.dateFrom);
    if (filter.dateTo)       q.set('dateTo',  filter.dateTo);

    const res = await api.get<ApiResponse<PurchaseOrdersPagedResult>>(
      `/api/purchases/orders?${q.toString()}`
    );
    return res.data.responseObject ?? { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };
  },

  async getById(id: string): Promise<PurchaseOrderDetail | null> {
    const res = await api.get<ApiResponse<PurchaseOrderDetail | null>>(
      `/api/purchases/orders/${id}`
    );
    return res.data.responseObject ?? null;
  },

  async getPendientesPorFacturar(): Promise<PurchaseOrder[]> {
    const res = await api.get<ApiResponse<PurchaseOrder[]>>(
      '/api/purchases/orders/pendientes-por-facturar'
    );
    return res.data.responseObject ?? [];
  },

  async crear(payload: CreatePurchaseOrderRequest): Promise<PurchaseOrder> {
    const res = await api.post<ApiResponse<PurchaseOrder>>(
      '/api/purchases/orders',
      payload
    );
    return res.data.responseObject;
  },

  async enviar(id: string): Promise<PurchaseOrder> {
    const res = await api.patch<ApiResponse<PurchaseOrder>>(
      `/api/purchases/orders/${id}/enviar`
    );
    return res.data.responseObject;
  },

  async aprobar(id: string): Promise<PurchaseOrder> {
    const res = await api.patch<ApiResponse<PurchaseOrder>>(
      `/api/purchases/orders/${id}/aprobar`
    );
    return res.data.responseObject;
  },

  async cancelar(id: string): Promise<PurchaseOrder> {
    const res = await api.patch<ApiResponse<PurchaseOrder>>(
      `/api/purchases/orders/${id}/cancelar`
    );
    return res.data.responseObject;
  },

  async vincularFactura(id: string, purchBillId: string): Promise<PurchaseOrder> {
    const res = await api.post<ApiResponse<PurchaseOrder>>(
      `/api/purchases/orders/${id}/vincular-factura`,
      { purchBillId }
    );
    return res.data.responseObject;
  },
