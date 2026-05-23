import { test, expect } from '@playwright/test';
import {
  API_BASE,
  apiReachable,
  DEMO_EMAIL,
  DEMO_PASSWORD,
  login,
  listMyCompanies,
  switchCompany,
  refreshSession,
  searchBusinessPartners,
} from './helpers/api';
import {
  PLATFORM_EMAIL,
  PLATFORM_PASSWORD,
  loginPlatform,
  listPlatformSubscribers,
  switchSubscriberApi,
} from './helpers/platform';

test.describe('Phase 3 — Platform smoke', () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test('platform API: login + subscribers list', async ({ request }) => {
    const session = await loginPlatform(request);
    expect(session.token.length).toBeGreaterThan(10);
    const subs = await listPlatformSubscribers(request, session.token);
    expect(subs.length).toBeGreaterThan(0);
  });

  test('platform UI: login → overview → subscribers', async ({ page }) => {
    test.setTimeout(90_000);
    await page.goto('/login');
    await page.locator('#lp-email').fill(PLATFORM_EMAIL);
    await page.locator('#lp-password').fill(PLATFORM_PASSWORD);
    await page.getByRole('button', { name: /Iniciar sesión/i }).click();
    await page.waitForURL(/\/platform\/(overview|subscribers)/, { timeout: 45_000 });
    await page.goto('/platform/subscribers');
    await expect(page.getByRole('heading', { name: /Suscriptores|Subscribers/i })).toBeVisible({ timeout: 30_000 });
  });

  test('impersonation API: platform → switch-subscriber → tenant context', async ({ request }) => {
    const platform = await loginPlatform(request);
    const subs = await listPlatformSubscribers(request, platform.token);
    const target = subs[0];
    test.skip(!target?.id, 'Sin suscriptores seed');

    const tenant = await switchSubscriberApi(request, platform.token, target.id);
    expect(tenant.token.length).toBeGreaterThan(10);

    const companies = await listMyCompanies(request, tenant.token);
    expect(companies.length).toBeGreaterThan(0);
  });
});

test.describe('Phase 3 — Runtime smoke', () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test('tenant login + switch company isolation', async ({ request }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(companies.length < 2, 'Se requieren ≥2 empresas demo para aislamiento');

    const first = companies[0]!;
    const second = companies[1]!;
    const s1 = await switchCompany(request, session.token, first.companyId);
    const bp1 = await searchBusinessPartners(request, s1.token, { q: '' });

    const s2 = await switchCompany(request, session.token, second.companyId);
    const bp2 = await searchBusinessPartners(request, s2.token, { q: '' });

    expect(s1.companyId).not.toEqual(s2.companyId);
    expect(JSON.stringify(bp1)).not.toEqual(JSON.stringify(bp2));
  });

  test('refresh token preserves session', async ({ request }) => {
    const session = await login(request);
    const refreshed = await refreshSession(request, session.token);
    expect(refreshed.token.length).toBeGreaterThan(10);
    const companies = await listMyCompanies(request, refreshed.token);
    expect(companies.length).toBeGreaterThan(0);
  });

  test('forbidden cross-company access', async ({ request }) => {
    const session = await login(request);
    const res = await request.get(`${API_BASE}/api/companies/00000000-0000-0000-0000-000000000099`, {
      headers: { Authorization: `Bearer ${session.token}` },
    });
    expect(res.ok()).toBeFalsy();
  });
});

test.describe('Phase 3 — BusinessPartner smoke', () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test('BP search API returns rows or empty (no 500)', async ({ request }) => {
    const session = await login(request);
    const rows = await searchBusinessPartners(request, session.token, { q: 'a' });
    expect(Array.isArray(rows)).toBe(true);
  });
});

test.describe('Phase 3 — Tenant login UI', () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test('runtime happy path login UI', async ({ page }) => {
    await page.goto('/login');
    await page.locator('#lp-email').fill(DEMO_EMAIL);
    await page.locator('#lp-password').fill(DEMO_PASSWORD);
    await page.getByRole('button', { name: /Iniciar sesión/i }).click();
    await page.waitForURL(/\/(select-company|dashboard|platform)/, { timeout: 45_000 });
  });
});
