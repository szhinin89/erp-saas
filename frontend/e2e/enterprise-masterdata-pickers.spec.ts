import { test, expect } from '@playwright/test';
import {
  API_BASE,
  apiReachable,
  login,
  listMyCompanies,
  switchCompany,
  searchBusinessPartners,
  listLegacyCustomers,
} from './helpers/api';

test.describe('MasterData pickers (BusinessPartner API)', () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test('sales: business-partners search returns customers when permitted', async ({ request }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    let token = session.token;
    if (!session.companyId && companies[0]) {
      token = (await switchCompany(request, token, companies[0].companyId)).token;
    }

    const rows = await searchBusinessPartners(request, token, { isActive: true });
    test.skip(rows.length === 0, 'MasterData vacío o sin permiso masterdata.businesspartners.view');

    const customers = rows.filter((r) => r.isCustomer ?? r.IsCustomer);
    expect(customers.length).toBeGreaterThan(0);

    const withApiLink = customers.some(
      (bp) => (bp.legacyCustomerId ?? bp.LegacyCustomerId)?.length,
    );
    if (withApiLink) {
      expect(withApiLink).toBe(true);
    } else {
      const legacy = await listLegacyCustomers(request, token);
      test.skip(legacy.length === 0, 'Sin legacy ni legacyCustomerId en DTO');
    }
  });

  test('purchases: business-partners search returns suppliers when permitted', async ({ request }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    let token = session.token;
    if (!session.companyId && companies[0]) {
      token = (await switchCompany(request, token, companies[0].companyId)).token;
    }

    const rows = await searchBusinessPartners(request, token, { isActive: true });
    test.skip(rows.length === 0, 'MasterData vacío o sin permiso');

    const suppliers = rows.filter((r) => r.isSupplier ?? r.IsSupplier);
    expect(suppliers.length).toBeGreaterThan(0);
  });
});
