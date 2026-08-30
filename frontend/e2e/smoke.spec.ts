import { test, expect } from "@playwright/test";

test.describe("Smoke", () => {
  test("página de login carga y muestra el formulario", async ({ page }) => {
    await page.goto("/login");

    await expect(page.getByTestId("erp-brand-title")).toHaveText(
      "ZH Technologies",
    );
    await expect(
      page.getByText("Sistema de gestión empresarial"),
    ).toBeVisible();
    await expect(page.locator("#lp-username")).toBeVisible();
    await expect(page.locator("#lp-password")).toBeVisible();
    await expect(page.locator("button.zh-auth-submit")).toBeVisible();
  });
});
