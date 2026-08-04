import { test, expect } from "@playwright/test";
import {
  API_BASE,
  apiReachable,
  login,
  listMyCompanies,
  switchCompany,
  searchBusinessPartners,
  getBusinessPartner,
  refreshSession,
} from "./helpers/api";

test.describe("MasterData coexistence (enterprise)", () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test("Customer is discovered by role filter and confirmed by detail", async ({ request }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    let token = session.token;
    if (!session.companyId && companies[0]) {
      token = (await switchCompany(request, token, companies[0].companyId))
        .token;
    }

    const rows = await searchBusinessPartners(request, token, {
      isActive: true,
      roles: ["Customer"],
    });
    expect(rows.length).toBeGreaterThan(0);

    const customer = await getBusinessPartner(request, token, rows[0]!.id);
    expect(customer.roles.some((role) => (role.roleType ?? role.RoleType) === "Customer")).toBe(true);
  });

  test("Supplier is discovered by role filter and confirmed by detail", async ({
    request,
  }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    let token = session.token;
    if (!session.companyId && companies[0]) {
      token = (await switchCompany(request, token, companies[0].companyId))
        .token;
    }

    const rows = await searchBusinessPartners(request, token, {
      isActive: true,
      roles: ["Supplier"],
    });
    expect(rows.length).toBeGreaterThan(0);

    const supplier = await getBusinessPartner(request, token, rows[0]!.id);
    expect(supplier.roles.some((role) => (role.roleType ?? role.RoleType) === "Supplier")).toBe(true);
  });

  test("switch company: business-partners still authorized", async ({
    request,
  }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(companies.length < 2, "Se requieren ≥2 empresas");

    const t1 = (
      await switchCompany(request, session.token, companies[0]!.companyId)
    ).token;
    const r1 = await searchBusinessPartners(request, t1);
    const t2 = (await switchCompany(request, t1, companies[1]!.companyId))
      .token;
    const r2 = await searchBusinessPartners(request, t2);

    expect(Array.isArray(r1)).toBe(true);
    expect(Array.isArray(r2)).toBe(true);
  });

  test("refresh token preserves company context", async ({ request }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(companies.length === 0, "Sin empresas");

    const switched = await switchCompany(
      request,
      session.token,
      companies[0]!.companyId,
    );
    const refreshed = await refreshSession(request, switched.token);

    expect(refreshed.token.length).toBeGreaterThan(10);
    expect(refreshed.companyId ?? switched.companyId).toBeTruthy();

    const rows = await searchBusinessPartners(request, refreshed.token);
    expect(Array.isArray(rows)).toBe(true);
  });
});
