// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, cleanup, render, waitFor } from "@testing-library/react";
import { Outlet } from "react-router-dom";

/**
 * BP-NAV-GUARD-01 — compatibilidad del router de datos de App.tsx (`createBrowserRouter` con ruta
 * comodín sobre el árbol declarativo existente): guards (ProtectedRoute), redirecciones, rutas
 * desconocidas y el navegador global (`spaNavigate`) siguen funcionando.
 * Se monta la App real; solo se sustituyen el arranque de sesión, la configuración, el layout y las
 * páginas hoja (sus propias dependencias de red no forman parte de esta comprobación).
 */

vi.mock("./components/SessionBootstrap", () => ({
  SessionBootstrap: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));
vi.mock("./modules/config", () => ({
  ConfigProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));
vi.mock("./components/AppLayout", () => ({
  AppLayout: () => (
    <div data-testid="layout">
      <Outlet />
    </div>
  ),
}));
vi.mock("./modules/auth/pages/LoginPage", () => ({
  LoginPage: () => <div data-testid="page">login</div>,
}));
vi.mock("./modules/dashboard/pages/DashboardPage", () => ({
  DashboardPage: () => <div data-testid="page">dashboard</div>,
}));

const USER = {
  userId: "u1",
  email: "u@example.com",
  tenantId: "11111111-1111-1111-1111-111111111111",
  companyId: "22222222-2222-2222-2222-222222222222",
  role: "Admin",
} as never;

async function mountApp(path: string, authenticated: boolean) {
  window.history.pushState({}, "", path);
  vi.resetModules();
  const [{ useAuthStore }, { default: App }, { I18nProvider }, nav] = await Promise.all([
    import("./store/authStore"),
    import("./App"),
    import("./i18n/i18n"),
    import("./lib/navigation/globalNavigator"),
  ]);
  useAuthStore.setState({
    hasHydrated: true,
    isAuthenticated: authenticated,
    user: authenticated ? USER : null,
  });
  const view = render(
    <I18nProvider>
      <App />
    </I18nProvider>,
  );
  return { ...view, spaNavigate: nav.spaNavigate };
}

beforeEach(() => {
  window.history.replaceState({}, "", "/");
});
afterEach(() => cleanup());

describe("App — router de datos con ruta comodín", () => {
  it("la ruta pública se resuelve sin sesión", async () => {
    const { findByTestId } = await mountApp("/login", false);
    expect((await findByTestId("page")).textContent).toBe("login");
    expect(window.location.pathname).toBe("/login");
  });

  it("el guard ProtectedRoute redirige a /login una ruta protegida sin sesión", async () => {
    await mountApp("/suppliers", false);
    await waitFor(() => expect(window.location.pathname).toBe("/login"));
  });

  it("una ruta desconocida con sesión pasa por HomeRedirect y llega al dashboard dentro del layout", async () => {
    const { findByTestId } = await mountApp("/ruta-que-no-existe", true);
    await waitFor(() => expect(window.location.pathname).toBe("/dashboard"));
    expect((await findByTestId("page")).textContent).toBe("dashboard");
    expect(await findByTestId("layout")).toBeTruthy();
  });

  it("el navegador global (interceptores) navega por el router sin recargar", async () => {
    const { findByTestId, spaNavigate } = await mountApp("/dashboard", true);
    await findByTestId("page");
    await act(async () => spaNavigate("/login"));
    await waitFor(() => expect(window.location.pathname).toBe("/login"));
    expect((await findByTestId("page")).textContent).toBe("login");
  });
});
