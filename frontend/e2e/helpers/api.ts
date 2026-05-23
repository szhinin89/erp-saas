import type { APIRequestContext } from '@playwright/test';

export const API_BASE = process.env.E2E_API_URL ?? 'http://localhost:5003';
export const DEMO_EMAIL = process.env.E2E_EMAIL ?? 'admin@erp.com';
export const DEMO_PASSWORD = process.env.E2E_PASSWORD ?? 'Admin123!';

export type AuthPayload = {
  token: string;
  companyId?: string;
  subscriberId?: string;
};

export async function apiReachable(request: APIRequestContext): Promise<boolean> {
  try {
    const res = await request.get(`${API_BASE}/health/live`, { timeout: 5_000 });
    return res.ok();
  } catch {
    return false;
  }
}

export async function login(request: APIRequestContext): Promise<AuthPayload> {
  const res = await request.post(`${API_BASE}/api/auth/login`, {
    data: { email: DEMO_EMAIL, password: DEMO_PASSWORD },
  });
  if (!res.ok()) {
    throw new Error(`login failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = body.data ?? body.Data ?? body.responseObject ?? body.ResponseObject;
  return {
    token: (data.token ?? data.Token) as string,
    companyId: (data.companyId ?? data.CompanyId) as string | undefined,
    subscriberId: (data.subscriberId ?? data.SubscriberId) as string | undefined,
  };
}

export async function switchCompany(
  request: APIRequestContext,
  token: string,
  companyId: string,
): Promise<AuthPayload> {
  const res = await request.post(`${API_BASE}/api/auth/switch-company`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { companyId },
  });
  if (!res.ok()) {
    throw new Error(`switch-company failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = body.data ?? body.Data ?? body.responseObject ?? body.ResponseObject;
  return {
    token: (data.token ?? data.Token) as string,
    companyId: (data.companyId ?? data.CompanyId) as string | undefined,
    subscriberId: (data.subscriberId ?? data.SubscriberId) as string | undefined,
  };
}

export async function listMyCompanies(request: APIRequestContext, token: string) {
  const res = await request.get(`${API_BASE}/api/auth/my-companies`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) {
    throw new Error(`my-companies failed: ${res.status()}`);
  }
  const body = await res.json();
  const data = body.data ?? body.Data ?? body.responseObject ?? body.ResponseObject;
  return data as Array<{ companyId: string; displayName: string }>;
}

export async function refreshSession(
  request: APIRequestContext,
  token: string,
): Promise<AuthPayload> {
  const res = await request.post(`${API_BASE}/api/auth/refresh`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {},
  });
  if (!res.ok()) {
    throw new Error(`refresh failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = body.data ?? body.Data ?? body.responseObject ?? body.ResponseObject;
  return {
    token: (data.token ?? data.Token) as string,
    companyId: (data.companyId ?? data.CompanyId) as string | undefined,
    subscriberId: (data.subscriberId ?? data.SubscriberId) as string | undefined,
  };
}

type BpRow = {
  id: string;
  identificationNumber?: string;
  IdentificationNumber?: string;
  isCustomer?: boolean;
  IsCustomer?: boolean;
  isSupplier?: boolean;
  IsSupplier?: boolean;
  legacyCustomerId?: string | null;
  LegacyCustomerId?: string | null;
  legacySupplierId?: string | null;
  LegacySupplierId?: string | null;
};

export async function getEntitlements(request: APIRequestContext, token: string) {
  const res = await request.get(`${API_BASE}/api/subscribers/entitlements/me`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) {
    throw new Error(`entitlements failed: ${res.status()}`);
  }
  const body = await res.json();
  const data = body.data ?? body.Data ?? body.responseObject ?? body.ResponseObject;
  return data as { enabledFeatures?: string[]; EnabledFeatures?: string[] };
}

export async function searchBusinessPartners(
  request: APIRequestContext,
  token: string,
  params: { q?: string; isActive?: boolean } = {},
): Promise<BpRow[]> {
  const q = new URLSearchParams();
  if (params.q?.trim()) q.set('q', params.q.trim());
  if (params.isActive !== undefined) q.set('isActive', String(params.isActive));
  const qs = q.toString();
  const res = await request.get(
    `${API_BASE}/api/master/business-partners${qs ? `?${qs}` : ''}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  if (res.status() === 403 || res.status() === 404) {
    return [];
  }
  if (!res.ok()) {
    throw new Error(`business-partners search failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = body.data ?? body.Data ?? body.responseObject ?? body.ResponseObject;
  return (data ?? []) as BpRow[];
}

export async function listLegacyCustomers(request: APIRequestContext, token: string) {
  const res = await request.get(`${API_BASE}/api/sales/customers?activeStatus=all`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) {
    throw new Error(`legacy customers failed: ${res.status()}`);
  }
  const body = await res.json();
  const data = body.data ?? body.Data ?? body.responseObject ?? body.ResponseObject;
  return (data ?? []) as Array<{ id: string; identificationNumber?: string; IdentificationNumber?: string }>;
}
