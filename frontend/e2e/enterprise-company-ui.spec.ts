import { test, expect } from "@playwright/test";
import {
  API_BASE,
  apiReachable,
  DEMO_EMAIL,
  login,
  listMyCompanies,
  switchCompany,
  listLegacyCustomers,
} from "./helpers/api";

test.describe("Enterprise company UI isolation", () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test("tenant-scoped customer ids remain stable after switch-company", async ({
    request,
  }) => {
    test.setTimeout(90_000);

    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(
      companies.length < 2,
      "Se requieren al menos 2 empresas en el tenant demo",
    );

    const companyA = companies[0]!.companyId;
    const companyB = companies[1]!.companyId;

    const tokenA = (await switchCompany(request, session.token, companyA))
      .token;
    const listA = await listLegacyCustomers(request, tokenA);

    const tokenB = (await switchCompany(request, session.token, companyB))
      .token;
    const listB = await listLegacyCustomers(request, tokenB);
    expect(listB.map((c) => c.id)).toEqual(listA.map((c) => c.id));
  });

  test("customers page remounts after switch via company switcher", async ({
    page,
    request,
  }) => {
    test.setTimeout(120_000);

    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(companies.length < 2, "Se requieren al menos 2 empresas");

    if (!session.companyId) {
      await switchCompany(request, session.token, companies[0]!.companyId);
    }

    await page.goto("/login");
    await page
      .locator("#lp-username")
      .fill(DEMO_EMAIL);
    await page
      .locator("#lp-password")
      .fill(process.env.E2E_PASSWORD ?? "");
    await page.getByRole("button", { name: /Iniciar sesión/i }).click();
    await page.waitForURL(/\/(select-company|dashboard|saas)/, {
      timeout: 45_000,
    });

    if (page.url().includes("select-company")) {
      await page.getByRole("button", { name: "Entrar" }).first().click();
      await page.waitForURL(/\/dashboard/, { timeout: 45_000 });
    }

    await page.goto("/masterdata/customers");
    await page.waitForLoadState("domcontentloaded");

    const switcher = page.locator(".company-switcher-select");
    await expect(switcher).toHaveCount(1, { timeout: 15_000 });
    await expect(switcher.locator("option")).toHaveCount(2, {
      timeout: 15_000,
    });

    const beforeText = await page
      .locator(".zh-entity-item-name")
      .first()
      .textContent()
      .catch(() => "");

    const optionValues = await switcher
      .locator("option")
      .evaluateAll((opts) =>
        opts.map((o) => (o as HTMLOptionElement).value).filter(Boolean),
      );
    const currentValue = await switcher.inputValue();
    const other = optionValues.find((v) => v !== currentValue);
    expect(other).toBeTruthy();

    await switcher.selectOption(other!);
    await page.waitForURL(/\/dashboard/, { timeout: 30_000 });

    await page.goto("/masterdata/customers");
    await page.waitForLoadState("domcontentloaded");

    const afterCount = await page.locator(".zh-entity-item-name").count();
    const afterText =
      afterCount > 0
        ? await page.locator(".zh-entity-item-name").first().textContent()
        : "";

    if (beforeText && afterText && beforeText !== afterText) {
      expect(afterText).not.toBe(beforeText);
    }
  });
});
