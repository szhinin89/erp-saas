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
import type { AccessibleCompany } from "../../../types/access";
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

function baseCompany(overrides: Partial<AccessibleCompany> = {}): AccessibleCompany {
  return {
    companyId: "company-2",
    tenantId: "tenant-1",
    legalName: "Empresa Dos S.A.",
    displayName: "Empresa Dos",
    ruc: "0999999999001",
    role: "Admin",
    isActive: true,
    operationalStatus: "Operational",
    taxRegime: null,
    isAccountingRequired: false,
    assignedBranchCount: 1,
    totalBranchCount: 1,
    ...overrides,
  };
}

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

    vi.mocked(authService.listMyCompanies).mockResolvedValue([baseCompany()]);
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

  it("renderiza título, subtítulo operativo, conteo y bloque de acceso seguro", async () => {
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();

    expect(screen.getByText("Selecciona una empresa")).toBeTruthy();
    expect(
      screen.getByText("Seleccione la empresa operativa para continuar"),
    ).toBeTruthy();
    expect(screen.getByText(/1\s+empresa disponible/)).toBeTruthy();
    expect(screen.getByText("Acceso seguro")).toBeTruthy();
    expect(screen.getByText("Está usando una sesión autenticada.")).toBeTruthy();
    expect(
      screen.getByText("Si no encuentra su empresa, contacte al administrador."),
    ).toBeTruthy();
  });

  it("renderiza el buscador con placeholder correcto y una card por empresa con RUC", async () => {
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();

    expect(
      screen.getByPlaceholderText("Buscar por nombre o RUC"),
    ).toBeTruthy();
    expect(screen.getByText(/RUC:\s*0999999999001/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Entrar" })).toBeTruthy();
  });

  it("renderiza rol, estado, sucursal (singular) y contabilidad reales del DTO", async () => {
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();

    expect(screen.getByText(/Rol:\s*Admin/)).toBeTruthy();
    expect(screen.getByText("Activa")).toBeTruthy();
    expect(screen.getByText("1 sucursal")).toBeTruthy();
    expect(screen.getByText("No lleva contabilidad")).toBeTruthy();
  });

  it("no inventa régimen tributario, logo ni última usada cuando el DTO no los trae", async () => {
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();

    expect(screen.queryByText(/RIMPE|Régimen/i)).toBeNull();
    expect(screen.queryByText(/Última usada/i)).toBeNull();
    expect(screen.queryByRole("img")).toBeNull();
    // Sin logoUrl: cae al avatar con inicial.
    expect(screen.getByText("E")).toBeTruthy();
  });

  it("renderiza sucursales en plural y régimen/contabilidad cuando el DTO los trae", async () => {
    vi.mocked(authService.listMyCompanies).mockResolvedValue([
      baseCompany({
        assignedBranchCount: 3,
        totalBranchCount: 5,
        taxRegime: "RIMPE",
        isAccountingRequired: true,
      }),
    ]);
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();
    expect(screen.getByText("3 sucursales")).toBeTruthy();
    expect(screen.getByText("RIMPE")).toBeTruthy();
    expect(screen.getByText("Lleva contabilidad")).toBeTruthy();
  });

  it("muestra 'Sin sucursales asignadas' cuando assignedBranchCount es 0", async () => {
    vi.mocked(authService.listMyCompanies).mockResolvedValue([
      baseCompany({ assignedBranchCount: 0, totalBranchCount: 4, role: "User" }),
    ]);
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();
    expect(screen.getByText("Sin sucursales asignadas")).toBeTruthy();
    // No bloquea el botón: el backend actual no impone esa regla.
    expect(
      (screen.getByRole("button", { name: "Entrar" }) as HTMLButtonElement).disabled,
    ).toBe(false);
  });

  it("muestra chip Suspendida cuando operationalStatus lo indica, sin bloquear el flujo", async () => {
    vi.mocked(authService.listMyCompanies).mockResolvedValue([
      baseCompany({ operationalStatus: "Suspended" }),
    ]);
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();
    expect(screen.getByText("Suspendida")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Entrar" })).toBeTruthy();
  });

  it("muestra chip Inactiva cuando isActive es false", async () => {
    vi.mocked(authService.listMyCompanies).mockResolvedValue([
      baseCompany({ isActive: false }),
    ]);
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();
    expect(screen.getByText("Inactiva")).toBeTruthy();
  });

  it("filtra empresas por nombre y por RUC, y muestra el empty state sin resultados", async () => {
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();
    const search = screen.getByPlaceholderText("Buscar por nombre o RUC");

    fireEvent.change(search, { target: { value: "Empresa Dos" } });
    expect(screen.getByText("Empresa Dos")).toBeTruthy();

    fireEvent.change(search, { target: { value: "0999999999001" } });
    expect(screen.getByText("Empresa Dos")).toBeTruthy();

    fireEvent.change(search, { target: { value: "no-existe" } });
    expect(
      screen.getByText("No se encontraron empresas con ese criterio."),
    ).toBeTruthy();
    expect(screen.queryByText("Empresa Dos")).toBeNull();
  });

  it("filtra empresas por rol y por régimen tributario", async () => {
    vi.mocked(authService.listMyCompanies).mockResolvedValue([
      baseCompany({ role: "Admin", taxRegime: "RIMPE" }),
      baseCompany({
        companyId: "company-3",
        displayName: "Empresa Tres",
        legalName: "Empresa Tres S.A.",
        ruc: "0999999999002",
        role: "User",
        taxRegime: "Régimen General",
      }),
    ]);
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();
    const search = screen.getByPlaceholderText("Buscar por nombre o RUC");

    fireEvent.change(search, { target: { value: "RIMPE" } });
    expect(screen.getByText("Empresa Dos")).toBeTruthy();
    expect(screen.queryByText("Empresa Tres")).toBeNull();

    fireEvent.change(search, { target: { value: "User" } });
    expect(screen.getByText("Empresa Tres")).toBeTruthy();
    expect(screen.queryByText("Empresa Dos")).toBeNull();
  });

  it("muestra el estado de carga inicial antes de resolver la lista", () => {
    vi.mocked(authService.listMyCompanies).mockReturnValue(
      new Promise(() => {}),
    );
    renderPage();

    expect(screen.getByText("Cargando empresas…")).toBeTruthy();
    expect(screen.queryByText("Empresa Dos")).toBeNull();
  });

  it("muestra un error técnico si falla la carga de empresas", async () => {
    vi.mocked(authService.listMyCompanies).mockRejectedValue(new Error("boom"));
    renderPage();

    expect(
      await screen.findByText("No se pudieron cargar las empresas."),
    ).toBeTruthy();
  });

  it("muestra el estado sin empresas asignadas y no ofrece botón Entrar", async () => {
    vi.mocked(authService.listMyCompanies).mockResolvedValue([]);
    renderPage();

    expect(await screen.findByText("No tienes empresas asignadas")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Entrar" })).toBeNull();
  });

  it("deshabilita el botón Entrar mientras la selección está en curso", async () => {
    let resolveSwitch: (value: AuthResponse) => void = () => {};
    vi.mocked(authService.switchCompany).mockReturnValue(
      new Promise((resolve) => {
        resolveSwitch = resolve;
      }),
    );
    renderPage();

    expect(await screen.findByText("Empresa Dos")).toBeTruthy();
    const enterBtn = screen.getByRole("button", {
      name: /Entrar/,
    }) as HTMLButtonElement;
    fireEvent.click(enterBtn);

    await waitFor(() => expect(enterBtn.disabled).toBe(true));

    resolveSwitch(authResponse);
    expect(await screen.findByText("Dashboard listo")).toBeTruthy();
  });

  it("mantiene el guard cuando no hay tenantId activo", () => {
    useAuthStore.setState({
      user: {
        userId: "user-1",
        fullName: "Ana Perez",
        username: "ana",
        email: "ana@test.com",
        role: "Admin",
        tenantId: null as unknown as string,
        companyId: null,
      },
    });
    renderPage();

    expect(
      screen.getByText(
        "No hay una sesión de selección activa. Inicia sesión nuevamente.",
      ),
    ).toBeTruthy();
    expect(screen.getByRole("button", { name: "Volver al inicio" })).toBeTruthy();
  });

  it("no usa clases ts-* heredadas sin estilos", async () => {
    const { container } = renderPage();
    expect(await screen.findByText("Empresa Dos")).toBeTruthy();

    const orphanClasses = Array.from(container.querySelectorAll("*")).some((el) =>
      Array.from(el.classList).some((cls) => cls.startsWith("ts-")),
    );
    expect(orphanClasses).toBe(false);
  });
});
