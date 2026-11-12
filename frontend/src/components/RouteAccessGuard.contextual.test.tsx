// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/i18n";
import { RouteAccessGuard, isContextualRouteAllowed, isRouteAllowed } from "./RouteAccessGuard";
import type { MainMenuGroup } from "./useAppLayoutNavigation";
import { useAuthStore } from "../store/authStore";
import { useSessionStore } from "../store/sessionStore";

/**
 * NAV-COVERAGE-H1 — el solicitante de efectivo (`supplier-payments.create`, sin permisos de Caja)
 * llega a "Mis solicitudes" y al detalle de su solicitud desde Pagos a proveedores. La excepción es
 * por ruta exacta + permiso efectivo: no abre /treasury ni la bandeja/acciones (que deciden la API
 * y los flags canFulfill/canReject del servidor).
 */
const REQUESTER_MENU: MainMenuGroup[] = [
  {
    id: "finance",
    label: "Finanzas",
    isActive: false,
    items: [{ id: "sp", to: "/supplier-payments", label: "Pagos a proveedores" }],
  },
];

const CASHIER_MENU: MainMenuGroup[] = [
  {
    id: "treasury",
    label: "Caja y Bancos",
    isActive: false,
    items: [{ id: "fr", to: "/treasury/cash/funding-requests", label: "Solicitudes de efectivo" }],
  },
];

const LIST = "/treasury/cash/funding-requests";
const DETAIL = "/treasury/cash/funding-requests/req-1";
const UNRELATED_TREASURY = [
  "/treasury/cash",
  "/treasury/cash/registers",
  "/treasury/cash/movement-reasons",
  "/treasury/banks/accounts",
  "/treasury/cash/funding-requests/req-1/fulfill",
  "/treasury/cash/funding-requests-admin",
];

function setSession(role: string, permissions: string[] | null) {
  useAuthStore.setState({ user: { role } as never });
  useSessionStore.setState({
    isLoaded: permissions !== null,
    authorization: permissions === null ? null : ({ permissions } as never),
  });
}

function renderAt(path: string, menu: MainMenuGroup[]) {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[path]}>
        <RouteAccessGuard mainMenuGroups={menu} sessionMenuResolved>
          <Routes>
            <Route path="*" element={<p>PAGINA MONTADA</p>} />
          </Routes>
        </RouteAccessGuard>
      </MemoryRouter>
    </I18nProvider>,
  );
}

afterEach(() => {
  cleanup();
  useAuthStore.setState({ user: null });
  useSessionStore.setState({ isLoaded: false, authorization: null });
});

describe("isContextualRouteAllowed — NAV-COVERAGE-H1", () => {
  it("solo las rutas exactas de lista y detalle con supplier-payments.create", () => {
    expect(isContextualRouteAllowed(LIST, ["supplier-payments.create"])).toBe(true);
    expect(isContextualRouteAllowed(`${LIST}?tab=mine`, ["supplier-payments.create"])).toBe(true);
    expect(isContextualRouteAllowed(DETAIL, ["supplier-payments.create"])).toBe(true);
  });

  it("sin el permiso del flujo no concede nada (fail-closed)", () => {
    expect(isContextualRouteAllowed(DETAIL, [])).toBe(false);
    expect(isContextualRouteAllowed(DETAIL, ["supplier-payments.view"])).toBe(false);
  });

  it.each(UNRELATED_TREASURY)("no abre la ruta de Caja y Bancos no relacionada '%s'", (path) => {
    expect(isContextualRouteAllowed(path, ["supplier-payments.create"])).toBe(false);
  });
});

describe("RouteAccessGuard — acceso contextual a solicitudes de efectivo", () => {
  it("el solicitante autorizado sin permisos de Caja monta su detalle y Mis solicitudes", () => {
    setSession("User", ["supplier-payments.create"]);
    expect(isRouteAllowed(DETAIL, REQUESTER_MENU)).toBe(false); // bloqueo previo reproducido
    renderAt(DETAIL, REQUESTER_MENU);
    expect(screen.getByText("PAGINA MONTADA")).toBeTruthy();
    cleanup();
    renderAt(`${LIST}?tab=mine`, REQUESTER_MENU);
    expect(screen.getByText("PAGINA MONTADA")).toBeTruthy();
  });

  it("el solicitante NO accede a otras pantallas de Caja y Bancos", () => {
    setSession("User", ["supplier-payments.create"]);
    for (const path of UNRELATED_TREASURY) {
      renderAt(path, REQUESTER_MENU);
      expect(screen.queryByText("PAGINA MONTADA"), path).toBeNull();
      cleanup();
    }
  });

  it("un usuario sin supplier-payments.create ni menú de Caja sigue bloqueado", () => {
    setSession("User", ["supplier-payments.view"]);
    renderAt(DETAIL, REQUESTER_MENU);
    expect(screen.queryByText("PAGINA MONTADA")).toBeNull();
  });

  it("sin permisos de sesión cargados no se concede la excepción", () => {
    setSession("User", null);
    renderAt(DETAIL, REQUESTER_MENU);
    expect(screen.queryByText("PAGINA MONTADA")).toBeNull();
  });

  it("el cajero conserva el acceso por menú a la bandeja y al detalle", () => {
    setSession("User", ["caja.funding-requests.view", "caja.funding-requests.fulfill"]);
    renderAt(LIST, CASHIER_MENU);
    expect(screen.getByText("PAGINA MONTADA")).toBeTruthy();
    cleanup();
    renderAt(DETAIL, CASHIER_MENU);
    expect(screen.getByText("PAGINA MONTADA")).toBeTruthy();
  });
});
