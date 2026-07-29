import { test, expect } from "@playwright/test";
import {
  API_BASE,
  apiReachable,
  DEMO_EMAIL,
  DEMO_PASSWORD,
  login,
  listMyCompanies,
  switchCompany,
} from "./helpers/api";

test.describe("Enterprise auth & limits", () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(
      !ok,
      `API no disponible en ${API_BASE} — inicie ERP.API y aplique migraciones`,
    );
  });

  test("happy path: login UI → select-company → dashboard", async ({
    page,
  }) => {
    test.setTimeout(60_000);
    await page.goto("/login");
    await page.locator("#lp-email").fill(DEMO_EMAIL);
    await page.locator("#lp-password").fill(DEMO_PASSWORD);
    await page.getByRole("button", { name: /Iniciar sesión/i }).click();

    await page.waitForURL(/\/(select-company|dashboard)/, { timeout: 30_000 });

    if (page.url().includes("select-company")) {
      await expect(
        page.getByRole("heading", { name: /Empresa operativa/i }),
      ).toBeVisible();
      await page.getByRole("button", { name: "Entrar" }).first().click();
      await page.waitForURL(/\/dashboard/, { timeout: 30_000 });
    }

    await expect(page).toHaveURL(/\/dashboard/);
  });

  test("create company blocked at MAX_COMPANIES (403)", async ({ request }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    expect(companies.length).toBeGreaterThan(0);

    let token = session.token;
    if (!session.companyId && companies[0]) {
      const switched = await switchCompany(
        request,
        token,
        companies[0].companyId,
      );
      token = switched.token;
    }

    const res = await request.post(`${API_BASE}/api/v1/companies`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        taxId: "1791234567001",
        legalName: "Empresa E2E Límite SA",
        mainAddress: "Quito",
      },
    });

    expect(res.status()).toBe(403);
    const raw = await res.text();
    if (raw.length > 0) {
      const body = JSON.parse(raw) as { message?: string; Message?: string };
      const message = (body.message ?? body.Message ?? "") as string;
      expect(message.toLowerCase()).toMatch(
        /límite|limit|empresa|companies|commercial/i,
      );
    }
  });

  test("forbidden: switch a empresa inexistente o sin acceso", async ({
    request,
  }) => {
    const session = await login(request);

    const foreignSwitch = await request.post(
      `${API_BASE}/api/v1/auth/switch-company`,
      {
        headers: { Authorization: `Bearer ${session.token}` },
        data: { companyId: "00000000-0000-0000-0000-000000000099" },
      },
    );
    expect(foreignSwitch.ok()).toBeFalsy();
    expect(foreignSwitch.status()).toBeGreaterThanOrEqual(400);

    const foreignCompanyGet = await request.get(
      `${API_BASE}/api/v1/companies/00000000-0000-0000-0000-000000000099`,
      { headers: { Authorization: `Bearer ${session.token}` } },
    );
    expect(foreignCompanyGet.ok()).toBeFalsy();
  });
});
