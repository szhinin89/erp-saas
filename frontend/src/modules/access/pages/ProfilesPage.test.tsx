// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { ProfilesPage } from "./ProfilesPage";
import { profileService } from "../api/profileService";

/**
 * ADMINISTRATION-CLEAN-ACCESS-01: ProfilesPage debe ser CRUD de perfiles puro (nombre/descripción/
 * estado) — la asignación de permisos vive en PermissionsAssignmentPage. Estos tests fijan esa
 * responsabilidad única (regresión: no debe volver a mezclarse el árbol de permisos aquí).
 */

const navigateMock = vi.fn();

vi.mock("react-router-dom", async () => {
  const actual =
    await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return { ...actual, useNavigate: () => navigateMock };
});

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

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <ProfilesPage />
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
  navigateMock.mockReset();
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(profileService.list).mockResolvedValue([PROFILE]);
  vi.mocked(profileService.create).mockReset();
  vi.mocked(profileService.update).mockReset();
  vi.mocked(profileService.getPermissions).mockReset();
  vi.mocked(profileService.upsertPermissions).mockReset();
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

describe("ProfilesPage — access gate", () => {
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

describe("ProfilesPage — lista y CRUD sin permisos embebidos", () => {
  it("lista perfiles cargados del servicio", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Ventas Jr.")).toBeTruthy();
    });
  });

  it("el modal de crear/editar no contiene ningún toggle de permisos", async () => {
    renderPage();

    await waitFor(() => expect(screen.getByText("Ventas Jr.")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: /Nuevo perfil/i }));

    await waitFor(() => {
      expect(screen.getByLabelText(/Nombre del perfil/i)).toBeTruthy();
    });

    // Guard de responsabilidad única: nada de switches de permisos ni catálogo de módulos aquí.
    expect(screen.queryAllByRole("switch").length).toBe(0);
    expect(screen.queryByText("Permisos del perfil")).toBeNull();
    expect(profileService.getPermissions).not.toHaveBeenCalled();
  });

  it('el botón "Gestionar permisos" navega a /admin/permissions con el perfil preseleccionado', async () => {
    renderPage();

    await waitFor(() => expect(screen.getByText("Ventas Jr.")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Gestionar permisos" }));

    expect(navigateMock).toHaveBeenCalledWith(
      `/admin/permissions?profileId=${PROFILE.id}`,
    );
  });

  it("crear perfil no envía permisos, solo nombre/descripción/estado", async () => {
    vi.mocked(profileService.create).mockResolvedValue({
      id: "new-profile",
      name: "Nuevo",
      description: null,
      isActive: true,
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Ventas Jr.")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: /Nuevo perfil/i }));
    await waitFor(() => screen.getByLabelText(/Nombre del perfil/i));

    fireEvent.change(screen.getByLabelText(/Nombre del perfil/i), {
      target: { value: "Nuevo" },
    });
    fireEvent.click(screen.getByRole("button", { name: /Guardar perfil/i }));

    await waitFor(() => {
      expect(profileService.create).toHaveBeenCalledWith("Nuevo", null);
    });
    expect(profileService.upsertPermissions).not.toHaveBeenCalled();
  });
});
