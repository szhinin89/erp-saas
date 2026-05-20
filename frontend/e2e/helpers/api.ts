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
