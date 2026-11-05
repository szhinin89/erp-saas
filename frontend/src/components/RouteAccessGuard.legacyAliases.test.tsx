// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { isValidElement, type ReactElement } from "react";
import { MemoryRouter, Navigate, Route, Routes, useLocation } from "react-router-dom";
import { I18nProvider } from "../i18n/i18n";
import { LEGACY_REDIRECT_ALIASES, RouteAccessGuard, isRouteAllowed } from "./RouteAccessGuard";
import type { MainMenuGroup } from "./useAppLayoutNavigation";
import {
  accessRoutes,
  adminCoreRoutes,
  catalogRoutes,
  companyManagementRoutes,
  mainRoutes,
  publicRoutes,
} from "../routes";
import { SupplierCreditDetailLegacyRedirect } from "../routes/legacyRedirects";

/**
 * NAV-COVERAGE-H4 — los redirects legacy registrados llegan a montar su <Navigate> aunque su primer
 * segmento no esté en el menú; el destino canónico sigue sujeto al menú (sin acceso funcional por
 * usar una URL antigua). Aliases exactos, nunca prefijos.
 */
type RouteEl = ReactElement<{ path?: string; element?: ReactElement<{ to?: string }> }>;

const ALL_ROUTES = [
  ...publicRoutes,
  ...mainRoutes,
  ...catalogRoutes,
  ...companyManagementRoutes,
  ...accessRoutes,
  ...adminCoreRoutes,
] as RouteEl[];

/** Redirects registrados: path legacy → destino (los dinámicos con :id usan su componente). */
const REGISTERED_REDIRECTS = new Map<string, { to: string; element: ReactElement }>();
for (const route of ALL_ROUTES) {
  if (!isValidElement(route)) continue;
  const { path, element } = route.props;
  if (!path || !element) continue;
  if (element.type === Navigate && typeof element.props.to === "string") {
    REGISTERED_REDIRECTS.set(path, { to: element.props.to, element });
  } else if (element.type === SupplierCreditDetailLegacyRedirect) {
    REGISTERED_REDIRECTS.set(path, { to: "/suppliers/credits/:id", element });
  }
}

const seg = (p: string) => `/${p.split("/").filter(Boolean)[0] ?? ""}`;
const concrete = (p: string) => p.replace(":id", "abc-123");
const menuWith = (to: string): MainMenuGroup[] => [
  { id: "g", label: "Grupo", isActive: false, items: [{ id: "i", to, label: "Destino" }] },
];
const UNRELATED_MENU = menuWith("/customers");

/** Redirects H4: [dominio, legacy, destino]. */
const H4_CASES: Array<[string, string, string]> = [
  ["Caja", "/cash", "/treasury/cash"],
  ["Caja", "/cash/registers", "/treasury/cash/registers"],
  ["SRI", "/settings/electronic-invoicing", "/sri/configuration/electronic-invoicing"],
  ["SRI", "/electronic-documents/monitor", "/sri/electronic-documents/monitor"],
  ["Productos", "/inventory/items", "/products/items"],
  ["Productos", "/inventory/item-types", "/products/item-types"],
  ["Contabilidad", "/sales/payment-methods", "/accounting/configuration/sales-collection-destinations"],
  ["Finanzas", "/finance/credit-terms", "/suppliers/credit-terms"],
  ["Finanzas", "/finance/supplier-credits", "/suppliers/credits"],
  ["Finanzas", "/finance/supplier-credits/:id", "/suppliers/credits/:id"],
  ["Administración", "/admin/users", "/access/users"],
];

function Where() {
  const loc = useLocation();
  return <p data-testid="where">{`${loc.pathname}${loc.search}${loc.hash}`}</p>;
}

function renderRedirect(legacy: string, url: string, destination: string, menu: MainMenuGroup[]) {
  const registered = REGISTERED_REDIRECTS.get(legacy)!;
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[url]}>
        <RouteAccessGuard mainMenuGroups={menu} sessionMenuResolved>
          <Routes>
            <Route path={legacy} element={registered.element} />
            <Route path={destination} element={<><p>DESTINO</p><Where /></>} />
          </Routes>
        </RouteAccessGuard>
      </MemoryRouter>
    </I18nProvider>,
  );
}

afterEach(cleanup);

describe("LEGACY_REDIRECT_ALIASES — solo redirects registrados (anti-deriva)", () => {
  it("cada alias es un redirect registrado en routes/*", () => {
    for (const alias of LEGACY_REDIRECT_ALIASES) {
      expect(REGISTERED_REDIRECTS.has(alias), alias).toBe(true);
    }
  });

  it("ningún redirect que cambia de segmento queda bloqueado si el destino está autorizado", () => {
    const crossSegment = [...REGISTERED_REDIRECTS].filter(([from, { to }]) => seg(from) !== seg(to));
    expect(crossSegment.length).toBeGreaterThanOrEqual(H4_CASES.length);
    for (const [from, { to }] of crossSegment) {
      expect(isRouteAllowed(concrete(from), menuWith(concrete(to))), `${from} → ${to}`).toBe(true);
    }
  });

  it("la tabla H4 coincide con los destinos registrados (no se alteran destinos)", () => {
    for (const [, legacy, destination] of H4_CASES) {
      expect(REGISTERED_REDIRECTS.get(legacy)?.to, legacy).toBe(destination);
    }
  });
});

describe.each(H4_CASES)("%s — %s → %s", (_domain, legacy, destination) => {
  it("usuario autorizado llega al destino canónico desde la URL antigua", async () => {
    renderRedirect(legacy, concrete(legacy), destination, menuWith(concrete(destination)));
    expect(await screen.findByText("DESTINO")).toBeTruthy();
    expect(screen.getByTestId("where").textContent).toBe(concrete(destination));
  });

  it("usuario no autorizado: el redirect monta pero el destino sigue bloqueado", async () => {
    renderRedirect(legacy, concrete(legacy), destination, UNRELATED_MENU);
    expect(await screen.findByText("Acceso restringido")).toBeTruthy();
    expect(screen.queryByText("DESTINO")).toBeNull();
  });

  it("query/hash en la URL antigua no eluden el destino ni cambian la decisión", async () => {
    renderRedirect(legacy, `${concrete(legacy)}?next=/admin#x`, destination, UNRELATED_MENU);
    expect(await screen.findByText("Acceso restringido")).toBeTruthy();
    expect(screen.queryByText("DESTINO")).toBeNull();
  });
});

describe("URLs no registradas no reciben la excepción", () => {
  it.each([
    "/cash/other",
    "/cash/registers/1",
    "/CASH",
    "/admin/users/1",
    "/admin/usersx",
    "/sales/payment-methods/new",
    "/finance/supplier-credits/a/b",
    "/finance/credit-terms-x",
    "/inventory/items/1",
    "/settings/electronic-invoicing/x",
    "/electronic-documents",
    "/electronic-documents/monitor/1",
  ])("'%s' sigue bloqueada sin menú del segmento", (path) => {
    expect(isRouteAllowed(path, UNRELATED_MENU)).toBe(false);
  });
});
