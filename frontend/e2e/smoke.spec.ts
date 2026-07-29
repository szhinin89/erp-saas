import { test, expect } from "@playwright/test";

test.describe("Smoke", () => {
  test("página de login carga y muestra el formulario", async ({ page }) => {
    await page.goto("/login");

    await expect(page.getByTestId("erp-brand-title")).toHaveText(
      "ZH Technologies",
    );
    await expect(
      page.getByText("Acceso al Portal ERP Corporativo"),
    ).toBeVisible();
    await expect(page.locator("#lp-email")).toBeVisible();
    await expect(page.locator("#lp-password")).toBeVisible();
    await expect(page.locator("button.lp-submit")).toBeVisible();
  });
});
