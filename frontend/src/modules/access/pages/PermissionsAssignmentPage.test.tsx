// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { PermissionsAssignmentPage } from "./PermissionsAssignmentPage";
import { profileService } from "../api/profileService";

/**
 * ADMINISTRATION-CLEAN-ACCESS-01: pantalla nueva, solo permisos por perfil. No crea usuarios ni
 * perfiles — reutiliza los mismos endpoints ya existentes de AccessProfilesController.
 */

vi.mock("../api/profileService", () => ({
  profileService: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    getPermissions: vi.fn(),
    upsertPermissions: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

const PROFILE = {
  id: "profile-1",
  name: "Ventas Jr.",
  description: "Perfil de ventas junior",
  isActive: true,
};

function renderPage(initialPath = "/admin/permissions") {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[initialPath]}>
        <PermissionsAssignmentPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

function setAuth(role: string | null) {
  useAuthStore.setState({
    user:
      role === null
        ? null
        : {
            userId: "user-1",
            fullName: "Test User",
            username: "test.user",
            email: "test@example.com",
            role,
            tenantId: "tenant-1",
          },
    isAuthenticated: role !== null,
    hasHydrated: true,
  });
}

beforeEach(() => {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(profileService.list).mockResolvedValue([PROFILE]);
  vi.mocked(profileService.getPermissions).mockResolvedValue({
    profileId: PROFILE.id,
    items: [
      { permissionKey: "masterdata.businesspartners.view", isAllowed: true },
    ],
  });
  vi.mocked(profileService.upsertPermissions).mockResolvedValue({
    saved: [],
    rejected: [],
    allSaved: true,
  });
  vi.mocked(profileService.create).mockReset();
  vi.mocked(profileService.update).mockReset();
  setAuth("Admin");
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

describe("PermissionsAssignmentPage — access gate", () => {
  it("sin permiso, no admin: renderiza NoAccessPage", () => {
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: () => false,
      has: () => false,
      isAdminRole: false,
    });

    renderPage();

    expect(
      screen.getAllByText("No tienes acceso a esta pantalla.").length,
    ).toBeGreaterThan(0);
  });
});

describe("PermissionsAssignmentPage — asignación por perfil", () => {
  it("carga el listado de perfiles al montar", async () => {
    renderPage();

    await waitFor(() => {
      expect(profileService.list).toHaveBeenCalled();
    });
  });

  it("sin perfil seleccionado, no carga permisos ni muestra el árbol de módulos", async () => {
    renderPage();

    await waitFor(() => expect(profileService.list).toHaveBeenCalled());

    expect(profileService.getPermissions).not.toHaveBeenCalled();
    expect(screen.queryByText("Permisos del perfil")).toBeNull();
  });

  it("preseleccionando ?profileId= carga los permisos de ese perfil", async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => {
      expect(profileService.getPermissions).toHaveBeenCalledWith(PROFILE.id);
    });
    await waitFor(() => {
      expect(screen.getByText("Permisos del perfil")).toBeTruthy();
    });
  });

  it("togglear un permiso y guardar llama a upsertPermissions con ese perfil", async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => {
      expect(screen.getByText("Permisos del perfil")).toBeTruthy();
    });

    const switches = screen.getAllByRole("switch");
    fireEvent.click(switches[0]);

    fireEvent.click(screen.getByRole("button", { name: /Guardar permisos/i }));

    await waitFor(() => {
      expect(profileService.upsertPermissions).toHaveBeenCalledWith(
        PROFILE.id,
        expect.any(Array),
      );
    });
  });

  it("no expone ningún campo de creación de usuario o perfil", async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => {
      expect(screen.getByText("Permisos del perfil")).toBeTruthy();
    });

    expect(screen.queryByLabelText(/Nombre del perfil/i)).toBeNull();
    expect(screen.queryByText(/Nuevo perfil/i)).toBeNull();
    expect(profileService.create).not.toHaveBeenCalled();
  });
});
