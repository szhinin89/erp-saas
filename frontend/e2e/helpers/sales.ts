import type { APIRequestContext } from '@playwright/test';
import { API_BASE } from './api';

function unwrap<T>(body: Record<string, unknown>): T {
  const data = (body.data ?? body.Data ?? body.responseObject ?? body.ResponseObject) as T;
  return data;
}

export async function listInvoices(
  request: APIRequestContext,
  token: string,
  pageSize = 50,
): Promise<{ items: Array<{ id: string }> }> {
  const res = await request.get(`${API_BASE}/api/sales/invoices?pageSize=${pageSize}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) {
    throw new Error(`list invoices failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = unwrap<{ items?: Array<{ id: string }>; Items?: Array<{ id: string }> }>(body);
  const items = (data.items ?? data.Items ?? []) as Array<{ id: string }>;
  return { items };
}

export async function getInvoice(
  request: APIRequestContext,
  token: string,
  invoiceId: string,
): Promise<{ ok: boolean; status: number }> {
  const res = await request.get(`${API_BASE}/api/sales/invoices/${invoiceId}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  return { ok: res.ok(), status: res.status() };
}

export async function getStockForSale(
  request: APIRequestContext,
  token: string,
  productId: string,
  warehouseId: string,
): Promise<number> {
  const res = await request.get(
    `${API_BASE}/api/sales/invoices/stock?productoId=${productId}&bodegaId=${warehouseId}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  if (!res.ok()) {
    throw new Error(`stock query failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = unwrap<{ availableQuantity?: number; AvailableQuantity?: number }>(body);
  return Number(data.availableQuantity ?? data.AvailableQuantity ?? 0);
}

export async function listWarehouses(
  request: APIRequestContext,
  token: string,
): Promise<Array<{ id: string }>> {
  const res = await request.get(`${API_BASE}/api/inventory/warehouses`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) {
    throw new Error(`warehouses failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = unwrap<Array<{ id: string }> | { items?: Array<{ id: string }> }>(body);
  if (Array.isArray(data)) return data;
  return (data.items ?? []) as Array<{ id: string }>;
}

export async function listCustomers(
  request: APIRequestContext,
  token: string,
): Promise<Array<{ id: string }>> {
  const res = await request.get(`${API_BASE}/api/sales/customers`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) {
    throw new Error(`customers failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = unwrap<Array<{ id: string }> | { items?: Array<{ id: string }> }>(body);
  if (Array.isArray(data)) return data;
  return (data.items ?? []) as Array<{ id: string }>;
}

export async function listProducts(
  request: APIRequestContext,
  token: string,
): Promise<Array<{ id: string }>> {
  const res = await request.get(`${API_BASE}/api/products?pageSize=20`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) {
    throw new Error(`products failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = unwrap<{ items?: Array<{ id: string }> }>(body);
  return (data.items ?? []) as Array<{ id: string }>;
}

export async function getBranches(
  request: APIRequestContext,
  token: string,
): Promise<Array<{ id: string }>> {
  const res = await request.get(`${API_BASE}/api/branches`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) {
    throw new Error(`branches failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = unwrap<Array<{ id: string }> | { items?: Array<{ id: string }> }>(body);
  if (Array.isArray(data)) return data;
  return (data.items ?? []) as Array<{ id: string }>;
}

export async function createInvoiceDraft(
  request: APIRequestContext,
  token: string,
  payload: {
    customerId: string;
    warehouseId: string;
    branchId: string;
    productId: string;
    quantity: number;
    unitPrice: number;
  },
): Promise<string> {
  const res = await request.post(`${API_BASE}/api/sales/invoices`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      customerId: payload.customerId,
      warehouseId: payload.warehouseId,
      branchId: payload.branchId,
      items: [
        {
          productId: payload.productId,
          quantity: payload.quantity,
          unitPrice: payload.unitPrice,
        },
      ],
    },
  });
  if (!res.ok()) {
    throw new Error(`create invoice failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = unwrap<string | { id?: string; Id?: string }>(body);
  if (typeof data === 'string') return data;
  return (data.id ?? data.Id)!;
}

export async function validateInvoice(
  request: APIRequestContext,
  token: string,
  invoiceId: string,
): Promise<void> {
  const res = await request.patch(`${API_BASE}/api/sales/invoices/${invoiceId}/validar`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) {
    throw new Error(`validate failed: ${res.status()} ${await res.text()}`);
  }
}

export async function emitInvoice(
  request: APIRequestContext,
  token: string,
  invoiceId: string,
): Promise<void> {
  const res = await request.patch(`${API_BASE}/api/sales/invoices/${invoiceId}/emitir`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) {
    throw new Error(`emit failed: ${res.status()} ${await res.text()}`);
  }
}
