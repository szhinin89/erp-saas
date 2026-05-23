import type { APIRequestContext } from '@playwright/test';
import { API_BASE, type AuthPayload } from './api';

export const PLATFORM_EMAIL = process.env.E2E_PLATFORM_EMAIL ?? 'superadmin@erp.com';
export const PLATFORM_PASSWORD = process.env.E2E_PLATFORM_PASSWORD ?? 'Admin123!';

function readPayload(body: Record<string, unknown>) {
  return (body.data ?? body.Data ?? body.responseObject ?? body.ResponseObject) as Record<string, unknown>;
}

export async function loginPlatform(request: APIRequestContext): Promise<AuthPayload> {
  const res = await request.post(`${API_BASE}/api/platform/auth/login`, {
    data: { email: PLATFORM_EMAIL, password: PLATFORM_PASSWORD },
  });
  if (!res.ok()) {
    throw new Error(`platform login failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = readPayload(body);
  return {
    token: (data.token ?? data.Token) as string,
    subscriberId: (data.subscriberId ?? data.SubscriberId) as string | undefined,
    companyId: (data.companyId ?? data.CompanyId) as string | undefined,
  };
}

export async function listPlatformSubscribers(request: APIRequestContext, token: string) {
  const res = await request.get(`${API_BASE}/api/platform/subscribers`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) throw new Error(`platform subscribers failed: ${res.status()}`);
  const body = await res.json();
  const data = readPayload(body);
  const list = (data.subscribers ?? data ?? []) as Array<{ id: string; name?: string; slug?: string }>;
  return Array.isArray(list) ? list : [];
}

export async function switchSubscriberApi(
  request: APIRequestContext,
  platformToken: string,
  subscriberId: string,
): Promise<AuthPayload> {
  const res = await request.post(`${API_BASE}/api/auth/switch-subscriber`, {
    headers: { Authorization: `Bearer ${platformToken}` },
    data: { subscriberId },
  });
  if (!res.ok()) throw new Error(`switch-subscriber failed: ${res.status()} ${await res.text()}`);
  const body = await res.json();
  const data = readPayload(body);
  return {
    token: (data.token ?? data.Token) as string,
    subscriberId: (data.subscriberId ?? data.SubscriberId) as string | undefined,
    companyId: (data.companyId ?? data.CompanyId) as string | undefined,
  };
}
