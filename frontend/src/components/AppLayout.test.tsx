// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/i18n";
import { useAuthStore } from "../store/authStore";
import { useActiveBranchStore } from "../store/activeBranchStore";
import { useSessionStore } from "../store/sessionStore";
import { accessService } from "../modules/auth/api/accessService";
import { authService } from "../modules/auth/api/authService";
import { sessionService } from "../modules/session/api/sessionService";
import { AppLayout } from "./AppLayout";

/**
 * ZH-APP-PAGE-SHELL-STANDARD-01 — AppLayout es el AppShell oficial: toda ruta
 * autenticada debe recibir la misma barra superior (ZHAppTenantHeader) y el
 * mismo contenedor de contenido, sin que cada pantalla la reimplemente.
 */

vi.mock("../modules/auth/api/accessService", () => ({
  accessService: { getSessionMenu: vi.fn() },
}));

vi.mock("../modules/auth/api/authService", () => ({
  authService: { listMyCompanies: vi.fn() },
}));

vi.mock("../modules/session/api/sessionService", () => ({
  sessionService: {
    getAvailableBranches: vi.fn(),
    switchBranch: vi.fn(),
    getContext: vi.fn(),
  },
}));

function renderAppLayoutAt(path: string) {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<AppLayout />}>
            <Route path={path} element={<div>CONTENIDO_DE_PRUEBA</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(accessService.getSessionMenu).mockResolvedValue([]);
  vi.mocked(authService.listMyCompanies).mockResolvedValue([]);
  vi.mocked(sessionService.getAvailableBranches).mockResolvedValue({
    loginMode: "AskBranch",
    defaultBranchId: null,
    branches: [],
  });

  useAuthStore.setState({
    user: {
      userId: "user-1",
      fullName: "Ana Perez",
      username: "ana",
      email: "ana@test.com",
      role: "Admin",
      tenantId: "tenant-1",
      companyId: "company-1",
    },
    isAuthenticated: true,
    hasHydrated: true,
  });
  // Sucursal ya resuelta: el gate de sucursal no debe interponerse frente al
  // contenido de la ruta en este test (eso se cubre en useBranchGate.test.ts).
  useActiveBranchStore.setState({
    branch: { id: "branch-1", name: "Matriz", isMainBranch: true },
  });
});

afterEach(() => {
  useAuthStore.setState({
    user: null,
    isAuthenticated: false,
    hasHydrated: false,
    token: null,
    companySessionVersion: 0,
  });
  useActiveBranchStore.setState({ branch: null });
  useSessionStore.setState({ isLoading: false, isLoaded: false });
});

describe("AppLayout — AppShell global", () => {
  it("renderiza la barra superior común (ZHAppTenantHeader) junto al contenido de la ruta", async () => {
    renderAppLayoutAt("/dashboard");

    expect(await screen.findByText("CONTENIDO_DE_PRUEBA")).toBeTruthy();
    // Barra superior: menú/launcher + acciones (buscador/notificaciones) + usuario.
    expect(document.querySelector(".zh-app-tenantHeader")).toBeTruthy();
    expect(document.querySelector(".zh-app-header__nav")).toBeTruthy();
    expect(document.querySelector(".zh-app-header__actions")).toBeTruthy();
  });

  it("mantiene la misma barra superior en una ruta distinta (no se reimplementa por pantalla)", async () => {
    renderAppLayoutAt("/purchases");

    expect(await screen.findByText("CONTENIDO_DE_PRUEBA")).toBeTruthy();
    expect(document.querySelector(".zh-app-tenantHeader")).toBeTruthy();
  });

  /**
   * ERP-CORE-BRANCH-GATE-FLICKER-01: regresión del flash de BranchSelectorModal confirmado
   * con instrumentación temporal — mientras GET /session/context sigue en vuelo (F5/bootstrap),
   * AppLayout no debe renderizar ni el contenido protegido NI el selector de sucursal, aunque
   * la sucursal ya esté persistida en UserSession y se vaya a resolver en milisegundos.
   */
  it("mientras session/context está cargando no muestra el contenido de la ruta ni el selector de sucursal", () => {
    useSessionStore.setState({ isLoading: true });
    useActiveBranchStore.setState({ branch: null });

    renderAppLayoutAt("/dashboard");

    expect(screen.queryByText("CONTENIDO_DE_PRUEBA")).toBeNull();
    expect(
      screen.queryByText("Seleccione una sucursal"),
    ).toBeNull();
  });

});
