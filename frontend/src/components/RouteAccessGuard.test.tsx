import { describe, expect, it } from "vitest";
import { isRouteAllowed } from "./RouteAccessGuard";
import type { MainMenuGroup } from "./useAppLayoutNavigation";

/**
 * URLS-MENU-ALIGNMENT-01: isRouteAllowed usa el primer segmento de la ruta para decidir si monta
 * la página. Cuando un NavItem se reubica (ej. /masterdata/customers -> /customers), el prefijo
 * viejo deja de ser producido por mainMenuGroups — sin registrarlo en LEGACY_REDIRECT_PREFIXES,
 * el <Navigate replace> legacy nunca llegaría a montar ni a redirigir (NoAccessPage lo bloquea
 * antes). Este test fija esa regla para los prefijos que este ticket dejó sin ningún NavItem
 * activo.
 */
describe("isRouteAllowed — legacy redirect prefixes (URLS-MENU-ALIGNMENT-01)", () => {
  const groupsWithoutLegacyPrefixes: MainMenuGroup[] = [
    {
      id: "customers",
      label: "Clientes",
      isActive: false,
      items: [{ id: "1", to: "/customers", label: "Clientes" }],
    },
    {
      id: "products",
      label: "Productos y servicios",
      isActive: false,
      items: [{ id: "2", to: "/products/items", label: "Productos" }],
    },
  ];

  it.each(["/masterdata/customers", "/catalog/tree", "/master/payment-terms", "/pricing"])(
    "permite montar la ruta legacy '%s' aunque ningún NavItem activo use ese prefijo",
    (legacyPath) => {
      expect(isRouteAllowed(legacyPath, groupsWithoutLegacyPrefixes)).toBe(true);
    },
  );

  it("sigue bloqueando un prefijo que no es ni un NavItem activo ni un alias legacy conocido", () => {
    expect(isRouteAllowed("/some-made-up-prefix", groupsWithoutLegacyPrefixes)).toBe(false);
  });

  it("permite la ruta nueva porque coincide con un NavItem real", () => {
    expect(isRouteAllowed("/customers", groupsWithoutLegacyPrefixes)).toBe(true);
    expect(isRouteAllowed("/products/items", groupsWithoutLegacyPrefixes)).toBe(true);
  });
});
