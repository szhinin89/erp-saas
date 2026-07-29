import { test, expect } from "@playwright/test";
import {
  API_BASE,
  DEMO_EMAIL,
  DEMO_PASSWORD,
  apiReachable,
} from "./helpers/api";

test.describe("Auth multi-tab refresh", () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test("reload paralelo en dos pestañas mantiene sesión", async ({
    browser,
  }) => {
    test.setTimeout(90_000);

    const context = await browser.newContext();
    const pageA = await context.newPage();
    const pageB = await context.newPage();

    for (const page of [pageA, pageB]) {
      await page.goto("/login");
      await page.locator("#lp-email").fill(DEMO_EMAIL);
      await page.locator("#lp-password").fill(DEMO_PASSWORD);
      await page.getByRole("button", { name: /Iniciar sesión/i }).click();
      await page.waitForURL(/\/(select-company|dashboard)/, {
        timeout: 45_000,
      });
      if (page.url().includes("select-company")) {
        await page.getByRole("button", { name: "Entrar" }).first().click();
        await page.waitForURL(/\/dashboard/, { timeout: 45_000 });
      }
    }

    await Promise.all([pageA.reload(), pageB.reload()]);
    await pageA.waitForURL(/\/dashboard/, { timeout: 45_000 });
    await pageB.waitForURL(/\/dashboard/, { timeout: 45_000 });

    await expect(pageA).toHaveURL(/\/dashboard/);
    await expect(pageB).toHaveURL(/\/dashboard/);

    await context.close();
  });

  test("logout en una pestaña cierra sesión en la otra", async ({
    browser,
  }) => {
    test.setTimeout(90_000);

    const context = await browser.newContext();
    const pageA = await context.newPage();
    const pageB = await context.newPage();

    await pageA.goto("/login");
    await pageA.locator("#lp-email").fill(DEMO_EMAIL);
    await pageA.locator("#lp-password").fill(DEMO_PASSWORD);
    await pageA.getByRole("button", { name: /Iniciar sesión/i }).click();
    await pageA.waitForURL(/\/(select-company|dashboard)/, { timeout: 45_000 });
    if (pageA.url().includes("select-company")) {
      await pageA.getByRole("button", { name: "Entrar" }).first().click();
      await pageA.waitForURL(/\/dashboard/, { timeout: 45_000 });
    }

    await pageB.goto("/dashboard");
    await pageB.waitForURL(/\/dashboard/, { timeout: 45_000 });

    await pageA
      .getByRole("button", { name: /Cerrar sesión|Sign out/i })
      .click();
    await pageA.waitForURL(/\/login/, { timeout: 20_000 });

    await pageB.waitForURL(/\/login/, { timeout: 20_000 }).catch(async () => {
      await pageB.reload();
      await pageB.waitForURL(/\/login/, { timeout: 20_000 });
    });

    await expect(pageB).toHaveURL(/\/login/);
    await context.close();
  });
});
