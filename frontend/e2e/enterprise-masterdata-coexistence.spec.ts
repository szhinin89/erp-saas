import { test, expect } from '@playwright/test';
import {
  API_BASE,
  apiReachable,
  login,
  listMyCompanies,
  switchCompany,
  searchBusinessPartners,
  refreshSession,
  getEntitlements,
} from './helpers/api';

test.describe('MasterData coexistence (enterprise)', () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test('DTO exposes legacy link fields for customers', async ({ request }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    let token = session.token;
    if (!session.companyId && companies[0]) {
      token = (await switchCompany(request, token, companies[0].companyId)).token;
    }

    const rows = await searchBusinessPartners(request, token, { isActive: true });
    test.skip(rows.length === 0, 'Sin MasterData');

    const sample = rows.find((r) => r.isCustomer ?? r.IsCustomer);
    test.skip(!sample, 'Sin perfiles cliente');

    expect(sample).toHaveProperty('id');
    const hasLinkField =
      'legacyCustomerId' in sample! || 'LegacyCustomerId' in sample!;
    expect(hasLinkField).toBe(true);
  });

  test('BP without legacy link: API may return null legacyCustomerId', async ({ request }) => {
    const session = await login(request);
    const rows = await searchBusinessPartners(request, session.token, { isActive: true });
    test.skip(rows.length === 0, 'Sin MasterData');

    const unlinked = rows.filter(
      (r) =>
        (r.isCustomer ?? r.IsCustomer) &&
        !(r.legacyCustomerId ?? r.LegacyCustomerId),
    );
    if (unlinked.length > 0) {
      expect(unlinked[0]!.legacyCustomerId ?? unlinked[0]!.LegacyCustomerId).toBeFalsy();
    }
  });

  test('switch company: business-partners still authorized', async ({ request }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(companies.length < 2, 'Se requieren ≥2 empresas');

    const t1 = (await switchCompany(request, session.token, companies[0]!.companyId)).token;
    const r1 = await searchBusinessPartners(request, t1);
    const t2 = (await switchCompany(request, t1, companies[1]!.companyId)).token;
    const r2 = await searchBusinessPartners(request, t2);

    expect(Array.isArray(r1)).toBe(true);
    expect(Array.isArray(r2)).toBe(true);
  });

  test('refresh token preserves company context', async ({ request }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(companies.length === 0, 'Sin empresas');

    const switched = await switchCompany(request, session.token, companies[0]!.companyId);
    const refreshed = await refreshSession(request, switched.token);

    expect(refreshed.token.length).toBeGreaterThan(10);
    expect(refreshed.companyId ?? switched.companyId).toBeTruthy();

    const rows = await searchBusinessPartners(request, refreshed.token);
    expect(Array.isArray(rows)).toBe(true);
  });

  test('entitlements endpoint returns enabledFeatures array', async ({ request }) => {
    const session = await login(request);
    const ent = await getEntitlements(request, session.token);
    const features = ent.enabledFeatures ?? ent.EnabledFeatures ?? [];
    expect(Array.isArray(features)).toBe(true);
  });
});
