// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/i18n";
import {
  INHERITED_CHILD_ROUTES,
  RouteAccessGuard,
  isRouteAllowed,
  resolveRoutePattern,
} from "./RouteAccessGuard";
import type { MainMenuGroup } from "./useAppLayoutNavigation";
import { useAuthStore } from "../store/authStore";
import { useSessionStore } from "../store/sessionStore";
import {
  PROTECTED_REDIRECT_PATHS,
  PROTECTED_ROUTE_PATHS,
} from "../routes/protectedRoutePaths";

/**
 * IAM-V1-F2 — el guard protege por la pantalla del menú (permisos efectivos), no por el primer
 * segmento: el detalle/alta/edición heredan de su pantalla; una hija con permiso propio nunca.
 */
const menu = (...routes: string[]): MainMenuGroup[] => [
  {
    id: "g",
    label: "Grupo",
    isActive: false,
    items: routes.map((to, i) => ({ id: String(i), to, label: to })),
  },
];

afterEach(() => {
  cleanup();
  useAuthStore.setState({ user: null });
  useSessionStore.setState({ isLoaded: false, isLoading: false, authorization: null });
});

describe("isRouteAllowed — pantalla del menú, no prefijo", () => {
  it("no hereda una pantalla hermana del mismo módulo (/settings/branches ⇏ /settings/company)", () => {
    expect(isRouteAllowed("/settings/branches", menu("/settings/branches"))).toBe(true);
    expect(isRouteAllowed("/settings/company", menu("/settings/branches"))).toBe(false);
  });

  it("no hereda hijas con permiso propio (/suppliers ⇏ /suppliers/credits, /sales ⇏ /sales/returns)", () => {
    expect(isRouteAllowed("/suppliers/credits", menu("/suppliers"))).toBe(false);
    expect(isRouteAllowed("/suppliers/credit-terms", menu("/suppliers"))).toBe(false);
    expect(isRouteAllowed("/sales/returns", menu("/sales"))).toBe(false);
    expect(isRouteAllowed("/treasury/cash/registers", menu("/treasury/cash"))).toBe(false);
  });

  it("el detalle, el alta y la edición heredan de su pantalla del menú", () => {
    expect(isRouteAllowed("/access/users/new", menu("/access/users"))).toBe(true);
    expect(isRouteAllowed("/access/users/abc-123", menu("/access/users"))).toBe(true);
    expect(isRouteAllowed("/suppliers/credits/abc-123", menu("/suppliers/credits"))).toBe(true);
    expect(isRouteAllowed("/companies/abc-123/edit", menu("/companies"))).toBe(true);
    expect(isRouteAllowed("/access/users/abc-123", menu("/admin/roles"))).toBe(false);
  });

  it("una hija con permiso propio no se confunde con un detalle (/suppliers/credits ≠ /suppliers/:id)", () => {
    expect(resolveRoutePattern("/suppliers/credits", PROTECTED_ROUTE_PATHS)).toBe(
      "/suppliers/credits",
    );
  });

  it("hijas que comparten permiso heredan solo de su padre declarado", () => {
    expect(isRouteAllowed("/purchases/returns", menu("/purchases"))).toBe(true);
    expect(isRouteAllowed("/initial-load/products", menu("/initial-load"))).toBe(true);
    expect(isRouteAllowed("/purchases/returns", menu("/sales"))).toBe(false);
  });

  it("dashboard siempre; una ruta desconocida nunca", () => {
    expect(isRouteAllowed("/dashboard", menu())).toBe(true);
    expect(isRouteAllowed("/settings/company", menu())).toBe(false);
    expect(isRouteAllowed("/no-existe/para-nadie", menu("/customers"))).toBe(false);
  });

  it("las pantallas de IAM exigen su propia entrada del menú", () => {
    const operativo = menu("/products/items", "/settings/branches", "/customers");
    for (const path of ["/access/users", "/access/users/abc-123", "/admin/roles", "/admin/permissions"])
      expect(isRouteAllowed(path, operativo)).toBe(false);
  });
});

describe("inventario de rutas protegidas registradas", () => {
  it("cada hija heredada declara un patrón registrado", () => {
    for (const child of Object.keys(INHERITED_CHILD_ROUTES))
      expect(PROTECTED_ROUTE_PATHS).toContain(child);
  });

  it("con el menú completo, todas las rutas registradas son alcanzables", () => {
    // Menú completo = cada ruta registrada que no es redirect ni hija/detalle de otra.
    const concrete = (p: string) => p.replace(/:[^/]+/g, "abc-123");
    const fullMenu = menu(...PROTECTED_ROUTE_PATHS.filter((p) => !p.includes(":")));
    const unreachable = PROTECTED_ROUTE_PATHS.filter(
      (p) => p !== "*" && !isRouteAllowed(concrete(p), fullMenu),
    );
    expect(unreachable).toEqual([]);
  });

  it("un menú vacío solo deja montar dashboard y redirects", () => {
    const concrete = (p: string) => p.replace(/:[^/]+/g, "abc-123");
    const reachablePages = PROTECTED_ROUTE_PATHS.filter(
      (p) =>
        isRouteAllowed(concrete(p), menu()) &&
        p !== "/dashboard" &&
        !PROTECTED_REDIRECT_PATHS.has(p) &&
        !isLegacyOnly(p),
    );
    expect(reachablePages).toEqual([]);
  });
});

/** Rutas bajo prefijos legacy (solo redirects históricos) — permitidas para redirigir. */
function isLegacyOnly(path: string): boolean {
  return ["/inventario", "/logistica", "/configuracion", "/actividad", "/masterdata", "/catalog", "/master", "/pricing"].some(
    (prefix) => path === prefix || path.startsWith(`${prefix}/`),
  );
}

function renderGuard(path: string, groups: MainMenuGroup[], resolved: boolean) {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[path]}>
        <RouteAccessGuard mainMenuGroups={groups} sessionMenuResolved={resolved}>
          <Routes>
            <Route path="*" element={<p>PAGINA MONTADA</p>} />
          </Routes>
        </RouteAccessGuard>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("RouteAccessGuard — no monta antes de resolver sesión y menú", () => {
  it("menú sin resolver: no monta la página", () => {
    useAuthStore.setState({ user: { role: "User" } as never });
    useSessionStore.setState({ isLoaded: true, authorization: { permissions: [] } as never });
    renderGuard("/customers", menu("/customers"), false);
    expect(screen.queryByText("PAGINA MONTADA")).toBeNull();
  });

  it("sesión cargando: no monta la página aunque el menú esté resuelto", () => {
    useAuthStore.setState({ user: { role: "User" } as never });
    useSessionStore.setState({ isLoaded: false, isLoading: true, authorization: null });
    renderGuard("/customers", menu("/customers"), true);
    expect(screen.queryByText("PAGINA MONTADA")).toBeNull();
  });

  it("sesión que no pudo cargarse: usuario operativo sin acceso (fail-closed)", () => {
    useAuthStore.setState({ user: { role: "User" } as never });
    useSessionStore.setState({ isLoaded: false, isLoading: false, authorization: null });
    renderGuard("/customers", menu("/customers"), true);
    expect(screen.queryByText("PAGINA MONTADA")).toBeNull();
  });

  it("sesión y menú resueltos: monta la pantalla autorizada", () => {
    useAuthStore.setState({ user: { role: "User" } as never });
    useSessionStore.setState({ isLoaded: true, isLoading: false, authorization: { permissions: [] } as never });
    renderGuard("/customers", menu("/customers"), true);
    expect(screen.getByText("PAGINA MONTADA")).toBeTruthy();
  });
});
