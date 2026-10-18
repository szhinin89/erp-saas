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

    await page.addInitScript(() => localStorage.setItem("zh.erp.locale", "es"));
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
    await page.getByRole("tab", { name: /Listado/i }).click();
    await expect(page.getByRole("tab", { name: /Listado/i })).toHaveAttribute("aria-selected", "true");

    const switcher = page.getByRole("combobox", { name: "Cambiar empresa operativa" });
    await expect(switcher).toHaveCount(1, { timeout: 15_000 });
    await expect(switcher.getByRole("option")).toHaveCount(2, {
      timeout: 15_000,
    });

    const customerRow = page.getByRole("table").getByRole("row").filter({
      has: page.getByRole("cell", { name: "1791352688001", exact: true }),
    });
    const customerName = customerRow.getByRole("cell", { name: "Cliente E2E ZH", exact: true });
    await expect(customerName).toBeVisible();
    const beforeText = await customerName.textContent();
    expect(beforeText?.trim()).toBeTruthy();

    const optionValues = await switcher
      .getByRole("option")
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
    await expect(switcher).toHaveValue(other!);
    // The previous Listado state must not survive the remount for the new Company.
    await expect(page.getByRole("tab", { name: /Resumen/i })).toHaveAttribute("aria-selected", "true");
    await page.getByRole("tab", { name: /Listado/i }).click();
    await expect(page.getByRole("tab", { name: /Listado/i })).toHaveAttribute("aria-selected", "true");

    // BusinessPartner is tenant-wide: switching Company preserves customer identity.
    await expect(customerName).toBeVisible();
    await expect(customerName).toHaveText(beforeText!);
  });
});
