import { test, expect } from "@playwright/test";
import { createHmac, randomUUID } from "node:crypto";
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
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem("zh.erp.locale", "es"));
  });
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
    await page.locator("#lp-username").fill(DEMO_EMAIL);
    await page.locator("#lp-password").fill(DEMO_PASSWORD);
    await page.getByRole("button", { name: /Iniciar sesión/i }).click();

    await page.waitForURL(/\/(select-company|dashboard)/, { timeout: 30_000 });

    if (page.url().includes("select-company")) {
      await expect(
        page.getByRole("heading", { name: "Selecciona una empresa", exact: true }),
      ).toBeVisible();
      await page.getByRole("button", { name: "Entrar" }).first().click();
      await page.waitForURL(/\/dashboard/, { timeout: 30_000 });
    }

    await expect(page).toHaveURL(/\/dashboard/);
  });

  test("create company forbids a tenant administrator (403)", async ({
    request,
  }) => {
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
  });

  test("authorized provisioning validates the current DTO (400)", async ({ request }) => {
    // Same signed HTTP-test identity as backend TestJwtFactory: real JWT validation and
    // CompanyProvisioning policy, with an ephemeral key supplied only by the E2E harness.
    const secret = process.env.E2E_JWT_SECRET;
    if (!secret) throw new Error("E2E_JWT_SECRET must match the isolated E2E API signing key.");
    const encode = (value: object) => Buffer.from(JSON.stringify(value)).toString("base64url");
    const unsigned = `${encode({ alg: "HS256", typ: "JWT" })}.${encode({
      sub: randomUUID(), tenant_id: "00000000-0000-0000-0000-000000000000", role: "Admin",
      iss: "ZHTechnologies", aud: "ERPUsers", exp: Math.floor(Date.now() / 1000) + 300,
    })}`;
    const token = `${unsigned}.${createHmac("sha256", secret).update(unsigned).digest("base64url")}`;
    const res = await request.post(`${API_BASE}/api/v1/companies`, {
      headers: { Authorization: `Bearer ${token}` },
      // TaxId and LegalName are required strings in the current DTO. Invalid model
      // binding must return 400 before any provisioning or persistence takes place.
      data: { tenantId: randomUUID(), tradeName: null },
    });
    expect(res.status()).toBe(400);
    const body = (await res.json()) as {
      title?: string;
      errors?: Record<string, string[]>;
    };
    expect(body.title).toBe("One or more validation errors occurred.");
    expect(body.errors?.TaxId).toContain("The TaxId field is required.");
    expect(body.errors?.LegalName).toContain("The LegalName field is required.");
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
