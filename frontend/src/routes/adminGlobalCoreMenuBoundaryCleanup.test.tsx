import { describe, expect, it } from "vitest";
import { adminCoreRoutes } from "./adminCoreRoutes";
import { PROTECTED_ROUTE_PATHS } from "./protectedRoutePaths";

/**
 * Límite AdminCore vs ERP operativo: la creación y la identidad completa de empresas (RUC, estado)
 * son exclusivas de AdminGlobalCore (/admin-core/*); la empresa activa se configura solo en
 * /settings/company. El shell operativo no expone /companies ni /companies/:id/edit.
 */
describe("shell operativo — sin pantallas de administración de empresas", () => {
  it("no existen /companies, /companies/new ni /companies/:id/edit", () => {
    expect(PROTECTED_ROUTE_PATHS).not.toContain("/companies");
    expect(PROTECTED_ROUTE_PATHS).not.toContain("/companies/new");
    expect(PROTECTED_ROUTE_PATHS).not.toContain("/companies/:id/edit");
  });

  it("mantiene /settings/company como pantalla única de la empresa activa", () => {
    expect(PROTECTED_ROUTE_PATHS).toContain("/settings/company");
  });
});

describe("adminCoreRoutes — /admin-core/companies/new sigue activa", () => {
  const paths = adminCoreRoutes.map((route) => route.props.path);

  it("existe exactamente una vez /admin-core/companies/new", () => {
    expect(paths.filter((p) => p === "/admin-core/companies/new")).toHaveLength(1);
  });

  it("existe /admin-core/dashboard y /admin-core/system-provider-settings (sin regresión de otras rutas AdminCore)", () => {
    expect(paths).toContain("/admin-core/dashboard");
    expect(paths).toContain("/admin-core/system-provider-settings");
  });
});
