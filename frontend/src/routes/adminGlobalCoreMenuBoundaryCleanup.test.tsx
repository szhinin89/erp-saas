import { describe, expect, it } from "vitest";
import { companyManagementRoutes } from "./companyManagementRoutes";
import { adminCoreRoutes } from "./adminCoreRoutes";

/**
 * ZH-ADMINGLOBALCORE-MENU-BOUNDARY-CLEANUP-05O — guard de regresión: /companies/new (creación
 * de empresas) es exclusiva de AdminGlobalCore (/admin-core/companies/new). La pantalla
 * operativa quedó huérfana desde que ProtectedRoute redirige toda sesión global
 * (tenant_id==GLOBAL_TENANT_ID) fuera de AppLayout antes de poder alcanzarla — nadie podía
 * usarla. Si alguien reintrodujera la ruta operativa, este test fallaría.
 */
describe("companyManagementRoutes — límite AdminCore vs ERP operativo", () => {
  const paths = companyManagementRoutes.map((route) => route.props.path);

  it("no existe /companies/new en el shell operativo", () => {
    expect(paths).not.toContain("/companies/new");
  });

  it("mantiene /companies y /companies/:id/edit sin cambios (decisión aprobada: no se mueven)", () => {
    expect(paths).toContain("/companies");
    expect(paths).toContain("/companies/:id/edit");
    expect(paths).toHaveLength(2);
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
