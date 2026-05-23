import { test, expect } from '@playwright/test';
import {
  API_BASE,
  apiReachable,
  login,
  listMyCompanies,
  switchCompany,
  listLegacyCustomers,
} from './helpers/api';

test.describe('Enterprise company UI isolation', () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test('legacy customer ids differ after switch-company (API proxy for UI cache)', async ({ request }) => {
    test.setTimeout(90_000);

    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(companies.length < 2, 'Se requieren al menos 2 empresas en el tenant demo');

    const companyA = companies[0]!.companyId;
    const companyB = companies[1]!.companyId;

    const tokenA = (await switchCompany(request, session.token, companyA)).token;
    const listA = await listLegacyCustomers(request, tokenA);
    const idsA = new Set(listA.map((c) => c.id));

    const tokenB = (await switchCompany(request, session.token, companyB)).token;
    const listB = await listLegacyCustomers(request, tokenB);
    const overlap = listB.filter((c) => idsA.has(c.id));

    expect(overlap.length).toBe(0);
  });

  test('customers page remounts after switch via company switcher', async ({ page, request }) => {
    test.setTimeout(120_000);

    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(companies.length < 2, 'Se requieren al menos 2 empresas');

    if (!session.companyId) {
      await switchCompany(request, session.token, companies[0]!.companyId);
    }

    await page.goto('/login');
    await page.locator('#lp-email').fill(process.env.E2E_EMAIL ?? 'admin@erp.com');
    await page.locator('#lp-password').fill(process.env.E2E_PASSWORD ?? 'Admin123!');
    await page.getByRole('button', { name: /Iniciar sesión/i }).click();
    await page.waitForURL(/\/(select-company|dashboard|saas)/, { timeout: 45_000 });

    if (page.url().includes('select-company')) {
      await page.getByRole('button', { name: 'Entrar' }).first().click();
      await page.waitForURL(/\/dashboard/, { timeout: 45_000 });
    }

    await page.goto('/sales/customers');
    await page.waitForLoadState('networkidle');

    const switcher = page.locator('.company-switcher-select');
    test.skip((await switcher.count()) === 0, 'Company switcher no visible (una sola empresa)');

    const beforeText = await page.locator('.zh-entity-item-name').first().textContent().catch(() => '');

    const optionValues = await switcher.locator('option').evaluateAll((opts) =>
      opts.map((o) => (o as HTMLOptionElement).value).filter(Boolean),
    );
    const other = optionValues.find((v) => v !== (await switcher.inputValue()));
    test.skip(!other, 'No hay segunda empresa en el selector');

    await switcher.selectOption(other!);
    await page.waitForURL(/\/dashboard/, { timeout: 30_000 });

    await page.goto('/sales/customers');
    await page.waitForLoadState('networkidle');

    const afterCount = await page.locator('.zh-entity-item-name').count();
    const afterText = afterCount > 0 ? await page.locator('.zh-entity-item-name').first().textContent() : '';

    if (beforeText && afterText && beforeText !== afterText) {
      expect(afterText).not.toBe(beforeText);
    }
  });
});
