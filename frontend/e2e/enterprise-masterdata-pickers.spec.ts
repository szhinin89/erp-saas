import { test, expect } from "@playwright/test";
import {
  API_BASE,
  apiReachable,
  login,
  listMyCompanies,
  switchCompany,
  searchBusinessPartners,
  getBusinessPartner,
  listLegacyCustomers,
} from "./helpers/api";

test.describe("MasterData pickers (BusinessPartner API)", () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test("sales: business-partners search returns customers when permitted", async ({
    request,
  }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    let token = session.token;
    if (!session.companyId && companies[0]) {
      token = (await switchCompany(request, token, companies[0].companyId))
        .token;
    }

    const customers = await searchBusinessPartners(request, token, {
      isActive: true,
      roles: ["Customer"],
    });
    test.skip(
      customers.length === 0,
      "MasterData vacío o sin permiso masterdata.businesspartners.view",
    );

    expect(customers.length).toBeGreaterThan(0);

    const customer = await getBusinessPartner(request, token, customers[0]!.id);
    expect(customer.roles.some((role) => (role.roleType ?? role.RoleType) === "Customer")).toBe(true);

    const legacy = await listLegacyCustomers(request, token);
    test.skip(legacy.length === 0, "Sin legacy disponible para validar coexistencia");
  });

  test("purchases: business-partners search returns suppliers when permitted", async ({
    request,
  }) => {
    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    let token = session.token;
    if (!session.companyId && companies[0]) {
      token = (await switchCompany(request, token, companies[0].companyId))
        .token;
    }

    const suppliers = await searchBusinessPartners(request, token, {
      isActive: true,
      roles: ["Supplier"],
    });
    test.skip(suppliers.length === 0, "MasterData vacío o sin permiso");

    expect(suppliers.length).toBeGreaterThan(0);
    const supplier = await getBusinessPartner(request, token, suppliers[0]!.id);
    expect(supplier.roles.some((role) => (role.roleType ?? role.RoleType) === "Supplier")).toBe(true);
  });
});
