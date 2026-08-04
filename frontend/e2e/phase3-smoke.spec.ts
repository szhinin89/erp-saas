import { test, expect } from "@playwright/test";
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
} from "./helpers/api";

test.describe("Phase 3 — Runtime smoke", () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test("tenant login + switch company preserves tenant-scoped BusinessPartners", async ({ request }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(
      companies.length < 2,
      "Se requieren ≥2 empresas demo para aislamiento",
    );

    const first = companies[0]!;
    const second = companies[1]!;
    const s1 = await switchCompany(request, session.token, first.companyId);
    const bp1 = await searchBusinessPartners(request, s1.token, { q: "" });

    const s2 = await switchCompany(request, session.token, second.companyId);
    const bp2 = await searchBusinessPartners(request, s2.token, { q: "" });

    expect(s1.companyId).not.toEqual(s2.companyId);
    expect(bp1).toEqual(bp2);
  });

  test("refresh token preserves session", async ({ request }) => {
    const session = await login(request);
    const refreshed = await refreshSession(request, session.token);
    expect(refreshed.token.length).toBeGreaterThan(10);
    const companies = await listMyCompanies(request, refreshed.token);
    expect(companies.length).toBeGreaterThan(0);
  });

  test("forbidden cross-company access", async ({ request }) => {
    const session = await login(request);
    const res = await request.get(
      `${API_BASE}/api/v1/companies/00000000-0000-0000-0000-000000000099`,
      {
        headers: { Authorization: `Bearer ${session.token}` },
      },
    );
    expect(res.ok()).toBeFalsy();
  });
});

test.describe("Phase 3 — BusinessPartner smoke", () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test("BP search API returns rows or empty (no 500)", async ({ request }) => {
    const session = await login(request);
    const rows = await searchBusinessPartners(request, session.token, {
      q: "a",
    });
    expect(Array.isArray(rows)).toBe(true);
  });
});

test.describe("Phase 3 — Tenant login UI", () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test("runtime happy path login UI", async ({ page }) => {
    await page.goto("/login");
    await page.locator("#lp-username").fill(DEMO_EMAIL);
    await page.locator("#lp-password").fill(DEMO_PASSWORD);
    await page.getByRole("button", { name: /Iniciar sesión/i }).click();
    await page.waitForURL(/\/(select-company|dashboard)/, { timeout: 45_000 });
  });
});
