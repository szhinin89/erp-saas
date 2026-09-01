// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { loadDecimalConfig } from "../../../lib/config/decimal.config";
import { companyManagementService } from "../../company-management/api/companyManagementService";
import { useAuthStore } from "../../../store/authStore";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { useElectronicInvoicingStatusStore } from "../../../store/electronicInvoicingStatusStore";
import { useSessionStore } from "../../../store/sessionStore";
import type { AuthResponse } from "../../../types/auth";
import { authService } from "../api/authService";
import { CompanySelectPage } from "./CompanySelectPage";

vi.mock("../api/authService", () => ({
  authService: {
    listMyCompanies: vi.fn(),
    switchCompany: vi.fn(),
  },
}));

vi.mock("../../company-management/api/companyManagementService", () => ({
  companyManagementService: {
    getCurrent: vi.fn(),
  },
}));

vi.mock("../../../lib/config/decimal.config", () => ({
  loadDecimalConfig: vi.fn(),
}));

vi.mock("../../../lib/session/devSessionLog", () => ({
  logDevSessionContext: vi.fn(),
}));

const authResponse: AuthResponse = {
  userId: "user-1",
  fullName: "Ana Perez",
  username: "ana",
  email: "ana@test.com",
  role: "Admin",
  tenantId: "tenant-1",
  companyId: "company-2",
  token: "access-token",
  refreshToken: "refresh-token",
  refreshTokenExpiry: "2026-09-01T12:00:00Z",
};

const originalAuthLogin = useAuthStore.getState().login;
const originalSessionRefresh = useSessionStore.getState().refresh;
const originalElectronicStatusRefresh =
  useElectronicInvoicingStatusStore.getState().refresh;

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/select-company"]}>
        <Routes>
          <Route path="/select-company" element={<CompanySelectPage />} />
          <Route path="/dashboard" element={<div>Dashboard listo</div>} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("CompanySelectPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    localStorage.clear();

    useAuthStore.setState({
      user: {
        userId: "user-1",
        fullName: "Ana Perez",
        username: "ana",
        email: "ana@test.com",
        role: "Admin",
        tenantId: "tenant-1",
        companyId: null,
      },
      token: null,
      isAuthenticated: true,
      hasHydrated: true,
      companySessionVersion: 0,
      login: vi.fn((response: AuthResponse) => originalAuthLogin(response)),
    });
    useActiveBranchStore.setState({
      branch: {
        id: "old-branch",
        name: "Sucursal anterior",
        isMainBranch: false,
      },
    });

    useSessionStore.setState({
      refresh: vi.fn().mockImplementation(async () => {
        expect(useActiveBranchStore.getState().branch).toBeNull();
        useActiveBranchStore.getState().setBranch({
          id: "new-branch",
          name: "Matriz nueva",
          isMainBranch: true,
        });
      }),
    });
    useElectronicInvoicingStatusStore.setState({
      refresh: vi.fn().mockResolvedValue(undefined),
    });

    vi.mocked(authService.listMyCompanies).mockResolvedValue([
      {
        companyId: "company-2",
        tenantId: "tenant-1",
        legalName: "Empresa Dos S.A.",
        displayName: "Empresa Dos",
        ruc: "0999999999001",
        role: "Admin",
      },
    ]);
    vi.mocked(authService.switchCompany).mockResolvedValue(authResponse);
    vi.mocked(companyManagementService.getCurrent).mockResolvedValue(null);
    vi.mocked(loadDecimalConfig).mockResolvedValue({
      salesUnitPrice: 2,
      purchaseUnitPrice: 4,
      quantity: 4,
      percentage: 2,
      totalAmount: 2,
    });
  });

  afterEach(() => {
    cleanup();
    useAuthStore.setState({
      user: null,
      token: null,
      isAuthenticated: false,
      hasHydrated: false,
      companySessionVersion: 0,
      login: originalAuthLogin,
    });
    useActiveBranchStore.setState({ branch: null });
    useSessionStore.setState({
      refresh: originalSessionRefresh,
    });
    useElectronicInvoicingStatusStore.setState({
      refresh: originalElectronicStatusRefresh,
    });
    sessionStorage.clear();
    localStorage.clear();
  });

  it("sincroniza la sesión completa después de seleccionar empresa y navega al dashboard", async () => {
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Entrar" }));

    await waitFor(() => {
      expect(authService.switchCompany).toHaveBeenCalledWith("company-2");
    });
    expect(useAuthStore.getState().login).toHaveBeenCalledWith(authResponse);
    expect(useSessionStore.getState().refresh).toHaveBeenCalledTimes(1);
    expect(loadDecimalConfig).toHaveBeenCalledTimes(1);
    expect(
      useElectronicInvoicingStatusStore.getState().refresh,
    ).toHaveBeenCalledTimes(1);
    expect(companyManagementService.getCurrent).toHaveBeenCalledTimes(1);

    expect(await screen.findByText("Dashboard listo")).toBeTruthy();
    expect(useAuthStore.getState().user?.companyId).toBe("company-2");
    expect(useActiveBranchStore.getState().branch?.id).toBe("new-branch");
  });
});
