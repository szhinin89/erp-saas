// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor, cleanup } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { useAuthStore } from "../../../store/authStore";
import { AdminCoreProtectedRoute } from "../../../components/AdminCoreProtectedRoute";
import { systemProviderSettingsService } from "../api/systemProviderSettingsService";
import { accessService } from "../../auth/api/accessService";
import { sessionService } from "../../session/api/sessionService";
import { AdminCoreLayout } from "../components/AdminCoreLayout";
import { AdminCoreSystemProviderSettingsPage } from "./AdminCoreSystemProviderSettingsPage";

const GLOBAL_TENANT_ID = "00000000-0000-0000-0000-000000000000";

vi.mock("../api/systemProviderSettingsService", () => ({
  systemProviderSettingsService: { get: vi.fn(), update: vi.fn() },
}));

// Fase B: espías sobre endpoints operativos que esta pantalla nunca debe disparar.
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

function renderRoute() {
  return render(
    <MemoryRouter initialEntries={["/admin-core/system-provider-settings"]}>
      <Routes>
        <Route element={<AdminCoreProtectedRoute />}>
          <Route element={<AdminCoreLayout />}>
            <Route
              path="/admin-core/system-provider-settings"
              element={<AdminCoreSystemProviderSettingsPage />}
            />
          </Route>
        </Route>
        <Route path="/admin-core/login" element={<div>ADMIN_CORE_LOGIN</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

function setGlobalAdminSession() {
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
}

beforeEach(() => {
  vi.clearAllMocks();
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
});

describe("AdminCoreSystemProviderSettingsPage", () => {
  it("renderiza para AdminGlobalCore y carga la configuración actual", async () => {
    setGlobalAdminSession();
    vi.mocked(systemProviderSettingsService.get).mockResolvedValue({
      ruc: "1790012345001",
      legalName: "ZH Technologies",
      ciiuCode: "J6201",
      enabled: true,
      effectiveDate: "2026-01-01",
      isFullyConfigured: true,
      updatedAtUtc: "2026-01-01T00:00:00Z",
    });

    renderRoute();

    expect(await screen.findByDisplayValue("1790012345001")).toBeTruthy();
    expect(screen.getByDisplayValue("ZH Technologies")).toBeTruthy();
    expect(screen.getByDisplayValue("J6201")).toBeTruthy();

    expect(accessService.getSessionMenu).not.toHaveBeenCalled();
    expect(sessionService.getAvailableBranches).not.toHaveBeenCalled();
    expect(sessionService.getContext).not.toHaveBeenCalled();
  });

  it("bloquea a un AdminEmpresa (tenant real) redirigiendo a /admin-core/login", async () => {
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

    renderRoute();

    expect(await screen.findByText("ADMIN_CORE_LOGIN")).toBeTruthy();
    expect(systemProviderSettingsService.get).not.toHaveBeenCalled();
  });

  it("muestra un error controlado si la carga falla", async () => {
    setGlobalAdminSession();
    vi.mocked(systemProviderSettingsService.get).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 500,
        data: { message: { user: "El servidor no pudo procesar la solicitud." } },
      },
    });

    renderRoute();

    expect(
      await screen.findByText("El servidor no pudo procesar la solicitud."),
    ).toBeTruthy();
  });

  it("guarda cambios correctamente y muestra el mensaje de éxito", async () => {
    setGlobalAdminSession();
    vi.mocked(systemProviderSettingsService.get).mockResolvedValue({
      ruc: null,
      legalName: null,
      ciiuCode: null,
      enabled: false,
      effectiveDate: null,
      isFullyConfigured: false,
      updatedAtUtc: null,
    });
    vi.mocked(systemProviderSettingsService.update).mockResolvedValue({
      ruc: "1790012345001",
      legalName: "ZH Technologies",
      ciiuCode: "J6201",
      enabled: false,
      effectiveDate: null,
      isFullyConfigured: false,
      updatedAtUtc: "2026-01-01T00:00:00Z",
    });

    renderRoute();

    const rucInput = await screen.findByLabelText("RUC", { exact: false });
    fireEvent.change(rucInput, { target: { value: "1790012345001" } });
    fireEvent.change(screen.getByLabelText("Razón social", { exact: false }), {
      target: { value: "ZH Technologies" },
    });
    fireEvent.change(screen.getByLabelText("Código CIIU", { exact: false }), {
      target: { value: "J6201" },
    });

    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() => {
      expect(systemProviderSettingsService.update).toHaveBeenCalledWith({
        ruc: "1790012345001",
        legalName: "ZH Technologies",
        ciiuCode: "J6201",
        effectiveDate: null,
        enabled: false,
      });
    });

    expect(
      await screen.findByText(
        "Configuración del proveedor de sistema guardada correctamente.",
      ),
    ).toBeTruthy();
  });

  it("muestra un error controlado si el guardado falla", async () => {
    setGlobalAdminSession();
    vi.mocked(systemProviderSettingsService.get).mockResolvedValue({
      ruc: null,
      legalName: null,
      ciiuCode: null,
      enabled: false,
      effectiveDate: null,
      isFullyConfigured: false,
      updatedAtUtc: null,
    });
    vi.mocked(systemProviderSettingsService.update).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 500,
        data: { message: { user: "No se pudo guardar el cambio." } },
      },
    });

    renderRoute();

    await screen.findByLabelText("RUC", { exact: false });
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    expect(await screen.findByText("No se pudo guardar el cambio.")).toBeTruthy();
  });
});
