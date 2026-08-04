import type { APIRequestContext } from "@playwright/test";
import { API_BASE, companyHeaders } from "./api";

function unwrap<T>(body: Record<string, unknown>): T {
  return body.data as T;
}

export async function listInvoices(
  request: APIRequestContext,
  token: string,
  pageSize = 50,
  branchId?: string,
): Promise<{ items: Array<{ id: string }> }> {
  const res = await request.get(
    `${API_BASE}/api/v1/sales?pageNumber=1&pageSize=${pageSize}`,
    {
        headers: {
          ...companyHeaders(token),
          ...(branchId ? { "X-Branch-Id": branchId } : {}),
        },
    },
  );
  if (!res.ok()) {
    throw new Error(
      `list invoices failed: ${res.status()} ${await res.text()}`,
    );
  }
  const body = await res.json();
  const data = unwrap<{
    items?: Array<{ id: string }>;
    Items?: Array<{ id: string }>;
  }>(body);
  const items = (data.items ?? data.Items ?? []) as Array<{ id: string }>;
  return { items };
}

export async function getInvoice(
  request: APIRequestContext,
  token: string,
  invoiceId: string,
  branchId?: string,
): Promise<{ ok: boolean; status: number }> {
  const res = await request.get(
    `${API_BASE}/api/v1/sales/${invoiceId}`,
    {
      headers: {
        ...companyHeaders(token),
        ...(branchId ? { "X-Branch-Id": branchId } : {}),
      },
    },
  );
  return { ok: res.ok(), status: res.status() };
}

export async function getStockForSale(
  request: APIRequestContext,
  token: string,
  productId: string,
  warehouseId: string,
  branchId: string,
): Promise<number> {
  const res = await request.get(
    `${API_BASE}/api/v1/inventory/stock?itemId=${productId}&warehouseId=${warehouseId}`,
    { headers: { ...companyHeaders(token), "X-Branch-Id": branchId } },
  );
  if (!res.ok()) {
    throw new Error(`stock query failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = unwrap<Array<{
    availableQuantity?: number;
    AvailableQuantity?: number;
  }>>(body);
  const stock = data.find((x) => x.availableQuantity !== undefined || x.AvailableQuantity !== undefined);
  return Number(stock?.availableQuantity ?? stock?.AvailableQuantity ?? 0);
}

export async function listWarehouses(
  request: APIRequestContext,
  token: string,
): Promise<Array<{ id: string }>> {
  const res = await request.get(`${API_BASE}/api/v1/inventory/warehouses`, {
    headers: companyHeaders(token),
  });
  if (!res.ok()) {
    throw new Error(`warehouses failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = unwrap<
    Array<{ id: string }> | { items?: Array<{ id: string }> }
  >(body);
  if (Array.isArray(data)) return data;
  return (data.items ?? []) as Array<{ id: string }>;
}

export async function listCustomers(
  request: APIRequestContext,
  token: string,
): Promise<Array<{ id: string }>> {
  const res = await request.get(
    `${API_BASE}/api/v1/master/business-partners?type=customer&pageSize=100`,
    {
      headers: companyHeaders(token),
    },
  );
  if (!res.ok()) {
    throw new Error(`customers failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = unwrap<
    Array<{ id: string }> | { items?: Array<{ id: string }> }
  >(body);
  if (Array.isArray(data)) return data;
  return (data.items ?? []) as Array<{ id: string }>;
}

export async function listProducts(
  request: APIRequestContext,
  token: string,
): Promise<Array<{ id: string }>> {
  const res = await request.get(`${API_BASE}/api/v1/items?pageSize=20`, {
    headers: companyHeaders(token),
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
  const res = await request.get(`${API_BASE}/api/v1/settings/branches`, {
    headers: companyHeaders(token),
  });
  if (!res.ok()) {
    throw new Error(`branches failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = unwrap<
    Array<{ id: string }> | { items?: Array<{ id: string }> }
  >(body);
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
  const headers = { ...companyHeaders(token), "X-Branch-Id": payload.branchId };
  const pricingResponse = await request.get(
    `${API_BASE}/api/v1/sales/items/${payload.productId}/pricing`,
    { headers },
  );
  if (!pricingResponse.ok()) {
    throw new Error(`item pricing failed: ${pricingResponse.status()} ${await pricingResponse.text()}`);
  }
  const pricing = unwrap<{ unitPrice?: number; vatCode?: string; iceCode?: string }>(await pricingResponse.json());
  if (!pricing.vatCode) throw new Error("item pricing did not provide the required VAT code.");
  const draftPayload = {
      customerId: payload.customerId,
      issueDate: new Date().toISOString().slice(0, 10),
      lines: [
        { itemId: payload.productId, description: "Producto E2E Venta", quantity: payload.quantity, unitPrice: pricing.unitPrice ?? payload.unitPrice, vatCode: pricing.vatCode, iceCode: pricing.iceCode ?? null, warehouseId: payload.warehouseId },
      ],
  };
  const res = await request.post(`${API_BASE}/api/v1/sales`, {
    headers,
    data: draftPayload,
  });
  if (!res.ok()) {
    throw new Error(
      `create invoice failed: ${res.status()} ${await res.text()}`,
    );
  }
  const body = await res.json();
  const data = unwrap<{ id: string; grandTotal: number }>(body);
  const paymentMethods = await request.get(`${API_BASE}/api/v1/payment-methods?onlyActive=true`, { headers });
  const sriMethods = await request.get(`${API_BASE}/api/v1/catalog/sri-payment-methods`, { headers });
  if (!paymentMethods.ok() || !sriMethods.ok()) throw new Error("official payment catalogs failed.");
  const paymentMethod = (await paymentMethods.json()).data?.[0];
  const sriMethod = (await sriMethods.json()).data?.[0];
  if (!paymentMethod || !sriMethod) throw new Error("No active official E2E payment method.");
  const updated = await request.put(`${API_BASE}/api/v1/sales/${data.id}`, { headers, data: { id: data.id, ...draftPayload, sriPaymentMethodCode: sriMethod.code, payments: [{ paymentMethodId: paymentMethod.id, amount: data.grandTotal, reference: "E2E" }] } });
  if (!updated.ok()) throw new Error(`payment update failed: ${updated.status()} ${await updated.text()}`);
  return data.id;
}

export async function validateInvoice(
  request: APIRequestContext,
  token: string,
  invoiceId: string,
  branchId: string,
): Promise<void> {
  const res = await request.post(
    `${API_BASE}/api/v1/sales/${invoiceId}/authorize`,
    {
      headers: { ...companyHeaders(token), "X-Branch-Id": branchId },
    },
  );
  if (!res.ok()) {
    throw new Error(`validate failed: ${res.status()} ${await res.text()}`);
  }
}

export async function emitInvoice(
  request: APIRequestContext,
  token: string,
  invoiceId: string,
  branchId: string,
): Promise<void> {
  // The current sales contract performs commercial authorization and the
  // electronic-emission strategy in one operation; there is no second
  // /emitir endpoint after authorization.
  const res = await request.post(
    `${API_BASE}/api/v1/sales/${invoiceId}/authorize`,
    {
      headers: { ...companyHeaders(token), "X-Branch-Id": branchId },
    },
  );
  if (!res.ok()) {
    throw new Error(`emit failed: ${res.status()} ${await res.text()}`);
  }
}
