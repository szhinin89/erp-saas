import { describe, expect, it } from "vitest";
import { buildNavigationAccess } from "./navigationAccess";
import type { MainMenuGroup } from "./useAppLayoutNavigation";

/**
 * READINESS-2B-2 — destinos navegables derivados del menú autenticado (GET /api/v1/me/menu, ya
 * filtrado por el backend según el permiso de vista de cada NavItem). Sin mapa de permisos propio.
 */
describe("buildNavigationAccess", () => {
  const groups: MainMenuGroup[] = [
    {
      id: "treasury",
      label: "Tesorería",
      isActive: false,
      items: [
        {
          id: "cat",
          to: "",
          label: "Caja",
          children: [
            { id: "regs", to: "/treasury/cash/registers", label: "Cajas registradoras" },
          ],
        },
      ],
    },
    {
      id: "settings",
      label: "Configuración",
      isActive: false,
      items: [{ id: "company", to: "/settings/company", label: "Datos de la empresa" }],
    },
  ];

  it("encuentra un destino anidado y usa la etiqueta del menú", () => {
    const access = buildNavigationAccess(groups, true);
    expect(access.findDestination("/treasury/cash/registers")).toEqual({
      to: "/treasury/cash/registers",
      label: "Cajas registradoras",
    });
  });

  it("compara solo la ruta: conserva la query del destino (pestaña) para navegar", () => {
    const access = buildNavigationAccess(groups, true);
    expect(access.findDestination("/settings/company?tab=fiscal")).toEqual({
      to: "/settings/company?tab=fiscal",
      label: "Datos de la empresa",
    });
  });

  it("no considera disponible una ruta que el menú no incluye aunque comparta el primer segmento", () => {
    const access = buildNavigationAccess(groups, true);
    expect(access.findDestination("/settings/emission-points")).toBeNull();
    expect(access.findDestination("/treasury/cash/movement-reasons")).toBeNull();
  });

  it("sin menú resuelto no ofrece ningún destino (fail-closed)", () => {
    const access = buildNavigationAccess(groups, false);
    expect(access.resolved).toBe(false);
    expect(access.findDestination("/treasury/cash/registers")).toBeNull();
  });
});
