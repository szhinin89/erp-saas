import { test, expect, type Page } from "@playwright/test";
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

  async function selectBranchIfRequired(page: Page) {
    const dialog = page.getByRole("dialog", {
      name: /Seleccione una sucursal|Select a branch/i,
    });
    // El gate carga las sucursales después de que /dashboard ya está montado.
    // Esperar la aparición del diálogo real evita competir con ese flujo async.
    if (await dialog.isVisible({ timeout: 30_000 }).catch(() => false)) {
      await dialog.getByRole("button", { name: /Ingresar|Enter/i }).first().click();
      await dialog.waitFor({ state: "hidden", timeout: 20_000 });
    }
  }

  async function openUserMenuAfterBranchGate(page: Page) {
    const userMenu = page.getByRole("button", {
      name: /Menú de usuario|User menu/i,
    });
    try {
      await userMenu.click({ timeout: 3_000 });
      return;
    } catch (error) {
      const dialog = page.getByRole("dialog", {
        name: /Seleccione una sucursal|Select a branch/i,
      });
      if (!(await dialog.isVisible({ timeout: 10_000 }).catch(() => false))) {
        throw error;
      }
      await dialog.getByRole("button", { name: /Ingresar|Enter/i }).first().click();
      await dialog.waitFor({ state: "hidden", timeout: 20_000 });
      await userMenu.click();
    }
  }

  test("reload paralelo en dos pestañas mantiene sesión", async ({
    browser,
  }) => {
    test.setTimeout(90_000);

    const context = await browser.newContext();
    const pageA = await context.newPage();
    const pageB = await context.newPage();

    for (const page of [pageA, pageB]) {
      await page.goto("/login");
      await page.locator("#lp-username").fill(DEMO_EMAIL);
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
    await pageA.locator("#lp-username").fill(DEMO_EMAIL);
    await pageA.locator("#lp-password").fill(DEMO_PASSWORD);
    await pageA.getByRole("button", { name: /Iniciar sesión/i }).click();
    await pageA.waitForURL(/\/(select-company|dashboard)/, { timeout: 45_000 });
    if (pageA.url().includes("select-company")) {
      await pageA.getByRole("button", { name: "Entrar" }).first().click();
      await pageA.waitForURL(/\/dashboard/, { timeout: 45_000 });
    }

    await selectBranchIfRequired(pageA);

    await pageB.goto("/dashboard");
    await pageB.waitForURL(/\/dashboard/, { timeout: 45_000 });

    await selectBranchIfRequired(pageB);

    await openUserMenuAfterBranchGate(pageA);
    await pageA
      .getByRole("menuitem", { name: /Cerrar sesión|Sign out/i })
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
