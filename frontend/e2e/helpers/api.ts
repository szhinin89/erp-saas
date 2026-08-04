import type { APIRequestContext } from "@playwright/test";

export const API_BASE = process.env.E2E_API_URL ?? "http://localhost:5003";
export const DEMO_EMAIL = process.env.E2E_USERNAME ?? "e2e.admin";
export const DEMO_PASSWORD = process.env.E2E_PASSWORD ?? "";
const activeCompanyByToken = new Map<string, string>();

export function companyHeaders(token: string): Record<string, string> {
  const companyId = activeCompanyByToken.get(token);
  return companyId ? { Authorization: `Bearer ${token}`, "X-Company-Id": companyId } : { Authorization: `Bearer ${token}` };
}

export type AuthPayload = {
  token: string;
  companyId?: string;
  tenantId?: string;
};

export async function apiReachable(
  request: APIRequestContext,
): Promise<boolean> {
  try {
    const res = await request.get(`${API_BASE}/health/live`, {
      timeout: 5_000,
    });
    return res.ok();
  } catch {
    return false;
  }
}

export async function login(request: APIRequestContext): Promise<AuthPayload> {
  const res = await request.post(`${API_BASE}/api/v1/auth/login`, {
    data: { username: DEMO_EMAIL, password: DEMO_PASSWORD },
  });
  if (!res.ok()) {
    throw new Error(`login failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = body.data;
  const auth = {
    token: (data.token ?? data.Token) as string,
    companyId: (data.companyId ?? data.CompanyId) as string | undefined,
    tenantId: (data.tenantId ?? data.TenantId) as string | undefined,
  };
  if (auth.companyId) activeCompanyByToken.set(auth.token, auth.companyId);
  return auth;
}

export async function switchCompany(
  request: APIRequestContext,
  token: string,
  companyId: string,
): Promise<AuthPayload> {
  const res = await request.post(`${API_BASE}/api/v1/auth/switch-company`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { companyId },
  });
  if (!res.ok()) {
    throw new Error(
      `switch-company failed: ${res.status()} ${await res.text()}`,
    );
  }
  const body = await res.json();
  const data = body.data;
  const auth = {
    token: (data.token ?? data.Token) as string,
    companyId: (data.companyId ?? data.CompanyId) as string | undefined,
    tenantId: (data.tenantId ?? data.TenantId) as string | undefined,
  };
  if (auth.companyId) activeCompanyByToken.set(auth.token, auth.companyId);
  return auth;
}

export async function listMyCompanies(
  request: APIRequestContext,
  token: string,
) {
  const res = await request.get(`${API_BASE}/api/v1/auth/my-companies`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok()) {
    throw new Error(`my-companies failed: ${res.status()}`);
  }
  const body = await res.json();
  const data = body.data;
  return data as Array<{ companyId: string; displayName: string }>;
}

export async function refreshSession(
  request: APIRequestContext,
  token: string,
): Promise<AuthPayload> {
  const res = await request.post(`${API_BASE}/api/v1/auth/refresh`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {},
  });
  if (!res.ok()) {
    throw new Error(`refresh failed: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const data = body.data;
  return {
    token: (data.token ?? data.Token) as string,
    companyId: (data.companyId ?? data.CompanyId) as string | undefined,
    tenantId: (data.tenantId ?? data.TenantId) as string | undefined,
  };
}

type BpRow = {
  id: string;
  identificationNumber?: string;
  IdentificationNumber?: string;
};

type BpDetail = {
  id: string;
  roles: Array<{ roleType?: string; RoleType?: string }>;
};

function unwrapPagedRows<T>(data: unknown): T[] {
  if (Array.isArray(data)) return data as T[];
  if (data && typeof data === "object") {
    const page = data as { items?: unknown; Items?: unknown };
    if (Array.isArray(page.items)) return page.items as T[];
    if (Array.isArray(page.Items)) return page.Items as T[];
  }
  return [];
}

export async function searchBusinessPartners(
  request: APIRequestContext,
  token: string,
  params: { q?: string; isActive?: boolean; roles?: string[] } = {},
): Promise<BpRow[]> {
  const q = new URLSearchParams();
  if (params.q?.trim()) q.set("q", params.q.trim());
  if (params.isActive !== undefined) q.set("isActive", String(params.isActive));
  for (const role of params.roles ?? []) q.append("roles", role);
  const qs = q.toString();
  const res = await request.get(
    `${API_BASE}/api/v1/master/business-partners${qs ? `?${qs}` : ""}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  if (res.status() === 403 || res.status() === 404) {
    return [];
  }
  if (!res.ok()) {
    throw new Error(
      `business-partners search failed: ${res.status()} ${await res.text()}`,
    );
  }
  const body = await res.json();
  return unwrapPagedRows<BpRow>(body.data);
}

export async function getBusinessPartner(
  request: APIRequestContext,
  token: string,
  id: string,
): Promise<BpDetail> {
  const res = await request.get(
    `${API_BASE}/api/v1/master/business-partners/${id}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  if (!res.ok()) {
    throw new Error(
      `business-partner detail failed: ${res.status()} ${await res.text()}`,
    );
  }
  return (await res.json()).data as BpDetail;
}

export async function listLegacyCustomers(
  request: APIRequestContext,
  token: string,
) {
  const res = await request.get(
    `${API_BASE}/api/v1/master/business-partners?type=customer&pageSize=200`,
    {
      headers: { Authorization: `Bearer ${token}` },
    },
  );
  if (!res.ok()) {
    throw new Error(`customers failed: ${res.status()}`);
  }
  const body = await res.json();
  return unwrapPagedRows<{
    id: string;
    identificationNumber?: string;
    IdentificationNumber?: string;
  }>(body.data);
}
