import { test, expect } from '@playwright/test';

test.describe('Smoke', () => {
  test('página de login carga y muestra el formulario', async ({ page }) => {
    await page.goto('/login');

    await expect(page.locator('h2.zh-form-title').filter({ hasText: 'ERP SaaS' })).toBeVisible();
    await expect(page.getByText('Ingresa a tu cuenta')).toBeVisible();
    await expect(page.locator('#email')).toBeVisible();
    await expect(page.locator('#password')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Iniciar sesión' })).toBeVisible();
  });
});
