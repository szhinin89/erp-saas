// @vitest-environment jsdom
/**
 * EMP-E7 (T-14) — rutas de Empleados bajo AppLayout, visibilidad decidida por el menú del
 * servidor (RouteAccessGuard) e icono del módulo en el launcher.
 */
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render } from "@testing-library/react";
import { isValidElement } from "react";
import { isRouteAllowed } from "../components/RouteAccessGuard";
import { LauncherModuleIcon } from "../components/zh/header/launcher/LauncherIcon";
import type { MainMenuGroup } from "../components/useAppLayoutNavigation";
import { employeesRoutes } from "./employeesRoutes";
import { PROTECTED_ROUTE_PATHS } from "./protectedRoutePaths";

const PATHS = ["/employees", "/employees/catalogs", "/employees/:id"];

const menu = (...routes: string[]): MainMenuGroup[] => [
  {
    id: "employees",
    label: "Empleados",
    isActive: false,
    items: routes.map((to) => ({ id: to, to, label: to })),
  },
];

afterEach(cleanup);

describe("rutas de Empleados", () => {
  it("registra exactamente las tres pantallas y quedan protegidas", () => {
    expect(
      employeesRoutes.map((r) => (isValidElement<{ path: string }>(r) ? r.props.path : null)),
    ).toEqual(PATHS);
    expect(PROTECTED_ROUTE_PATHS).toEqual(expect.arrayContaining(PATHS));
  });

  it("sin ítem de menú no se monta ninguna (URL directa bloqueada)", () => {
    for (const url of ["/employees", "/employees/catalogs", "/employees/emp-1"]) {
      expect(isRouteAllowed(url, menu("/customers"))).toBe(false);
    }
  });

  it("con Empleados se abre la ficha, pero no los catálogos", () => {
    expect(isRouteAllowed("/employees", menu("/employees"))).toBe(true);
    expect(isRouteAllowed("/employees/emp-1", menu("/employees"))).toBe(true);
    expect(isRouteAllowed("/employees/catalogs", menu("/employees"))).toBe(false);
  });

  it("con solo Catálogos no se abren el listado ni la ficha", () => {
    expect(isRouteAllowed("/employees/catalogs", menu("/employees/catalogs"))).toBe(true);
    expect(isRouteAllowed("/employees", menu("/employees/catalogs"))).toBe(false);
    expect(isRouteAllowed("/employees/emp-1", menu("/employees/catalogs"))).toBe(false);
  });

  it("el launcher usa un icono oficial para el módulo", () => {
    const icon = (id: string) => {
      const { container, unmount } = render(<LauncherModuleIcon moduleId={id} />);
      const html = container.innerHTML;
      unmount();
      return html;
    };
    expect(icon("employees")).toBe(icon("masterdata"));
    expect(icon("employees")).not.toBe(icon("modulo-inexistente"));
  });
});
