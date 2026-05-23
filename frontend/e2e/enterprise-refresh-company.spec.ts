import { test, expect } from '@playwright/test';
import {
  API_BASE,
  apiReachable,
  login,
  listMyCompanies,
  switchCompany,
  refreshSession,
  listLegacyCustomers,
} from './helpers/api';

test.describe('Refresh token preserves company context', () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test('after refresh, customer list scope unchanged', async ({ request }) => {
    test.setTimeout(60_000);

    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(companies.length === 0, 'Sin empresas');

    const companyId = session.companyId ?? companies[0]!.companyId;
    const switched = await switchCompany(request, session.token, companyId);
    const before = await listLegacyCustomers(request, switched.token);
    const beforeIds = before.map((c) => c.id).sort().join(',');

    const refreshed = await refreshSession(request, switched.token);
    expect(refreshed.companyId).toBe(companyId);

    const after = await listLegacyCustomers(request, refreshed.token);
    const afterIds = after.map((c) => c.id).sort().join(',');

    expect(afterIds).toBe(beforeIds);
  });
});
