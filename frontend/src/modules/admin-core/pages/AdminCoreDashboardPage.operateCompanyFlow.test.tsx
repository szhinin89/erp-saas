// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { ProtectedRoute } from "../../../components/ProtectedRoute";
import { AdminCoreProtectedRoute } from "../../../components/AdminCoreProtectedRoute";
import { AppLayout } from "../../../components/AppLayout";
import { AdminCoreLayout } from "../components/AdminCoreLayout";
import { adminCoreService } from "../api/adminCoreService";
import { authService } from "../../auth/api/authService";
import { accessService } from "../../auth/api/accessService";
import { sessionService } from "../../session/api/sessionService";
import { AdminCoreDashboardPage } from "./AdminCoreDashboardPage";

const GLOBAL_TENANT_ID = "00000000-0000-0000-0000-000000000000";

vi.mock("../api/adminCoreService", () => ({
  adminCoreService: { listCompanies: vi.fn() },
}));

vi.mock("../../auth/api/authService", () => ({
  authService: { operateCompany: vi.fn(), returnToGlobal: vi.fn(), listMyCompanies: vi.fn() },
}));

vi.mock("../../auth/api/accessService", () => ({
  accessService: { getSessionMenu: vi.fn() },
}));

vi.mock("../../session/api/sessionService", () => ({
  sessionService: {
    getAvailableBranches: vi.fn(),
    switchBranch: vi.fn(),
    getContext: vi.fn(),
  },
}));

/**
 * Reproduce el árbol REAL de App.tsx (ProtectedRoute/AppLayout como hermano de
 * AdminCoreProtectedRoute/AdminCoreLayout, mismo orden) para detectar el bug reportado:
 * al operar una empresa desde /admin-core/dashboard, la app termina en /admin-core/login en
 * vez de /dashboard operativo.
 */
function renderFullTree() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/admin-core/dashboard"]}>
        <Routes>
          <Route element={<ProtectedRoute />}>
            <Route element={<AppLayout />}>
              <Route path="/dashboard" element={<div>DASHBOARD_OPERATIVO</div>} />
            </Route>
          </Route>

          <Route element={<AdminCoreProtectedRoute />}>
            <Route element={<AdminCoreLayout />}>
              <Route path="/admin-core/dashboard" element={<AdminCoreDashboardPage />} />
            </Route>
          </Route>

          <Route path="/admin-core/login" element={<div>ADMIN_CORE_LOGIN</div>} />
          <Route path="/login" element={<div>LOGIN_OPERATIVO</div>} />
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
      userId: "admin-1",
      fullName: "Global Admin",
      username: "global",
      email: null,
      role: "Admin",
      tenantId: GLOBAL_TENANT_ID,
      companyId: null,
    },
    isAuthenticated: true,
    hasHydrated: true,
  });
  useActiveBranchStore.setState({
    branch: { id: "branch-1", name: "Matriz", isMainBranch: true },
  });
});

afterEach(() => {
  cleanup();
  useAuthStore.setState({
    user: null,
    isAuthenticated: false,
    hasHydrated: false,
    token: null,
    companySessionVersion: 0,
  });
  useActiveBranchStore.setState({ branch: null });
});

describe("AdminGlobalCore → Ingresar a esta empresa (árbol real de rutas)", () => {
  it("tras operate-company termina en /dashboard operativo, no en /admin-core/login", async () => {
    vi.mocked(adminCoreService.listCompanies).mockResolvedValue([
      {
        tenantId: "tenant-a",
        tenantName: "Tenant A",
        tenantIsActive: true,
        companyId: "company-1",
        ruc: "1790012345001",
        legalName: "Empresa Uno",
        tradeName: null,
        isActive: true,
      },
    ]);
    vi.mocked(authService.operateCompany).mockResolvedValue({
      userId: "admin-1",
      fullName: "Global Admin",
      username: "global",
      email: null,
      role: "Admin",
      tenantId: "tenant-a",
      companyId: "company-1",
      requiresCompanySelection: false,
      token: "operative-jwt",
      operatorMode: true,
      globalAdminUserId: "admin-1",
    });

    renderFullTree();

    const button = await screen.findByRole("button", { name: "Ingresar a esta empresa" });
    fireEvent.click(button);

    await waitFor(() => {
      expect(authService.operateCompany).toHaveBeenCalledWith("company-1");
    });

    expect(await screen.findByText("DASHBOARD_OPERATIVO")).toBeTruthy();
    expect(screen.queryByText("ADMIN_CORE_LOGIN")).toBeNull();
    expect(useAuthStore.getState().user?.tenantId).toBe("tenant-a");
    expect(useAuthStore.getState().user?.companyId).toBe("company-1");

    // ProtectedRoute aceptó la sesión operativa (montó AppLayout, no rebotó) y AppLayout
    // muestra el banner de operador.
    expect(document.querySelector(".zh-app-tenantHeader")).toBeTruthy();
    expect(screen.getByText("AdminGlobalCore operando empresa")).toBeTruthy();
  });

  it("si operate-company falla, muestra un error controlado y se queda en AdminCore", async () => {
    vi.mocked(adminCoreService.listCompanies).mockResolvedValue([
      {
        tenantId: "tenant-a",
        tenantName: "Tenant A",
        tenantIsActive: true,
        companyId: "company-1",
        ruc: "1790012345001",
        legalName: "Empresa Uno",
        tradeName: null,
        isActive: true,
      },
    ]);
    vi.mocked(authService.operateCompany).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 403,
        data: { message: { user: "No autorizado como administrador global." } },
      },
    });

    renderFullTree();

    const button = await screen.findByRole("button", { name: "Ingresar a esta empresa" });
    fireEvent.click(button);

    expect(
      await screen.findByText("No autorizado como administrador global."),
    ).toBeTruthy();
    expect(screen.queryByText("DASHBOARD_OPERATIVO")).toBeNull();
    expect(screen.queryByText("ADMIN_CORE_LOGIN")).toBeNull();
    expect(useAuthStore.getState().user?.tenantId).toBe(GLOBAL_TENANT_ID);
  });
});
