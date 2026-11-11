// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, fireEvent, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { ProfilesPage } from "./ProfilesPage";
import { profileService } from "../api/profileService";
import { membershipService } from "../users/api/membershipService";
import { message } from "../../../lib/messages";

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

vi.mock("../users/api/membershipService", () => ({
  membershipService: { list: vi.fn() },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
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
  vi.mocked(profileService.getPermissions).mockReset().mockResolvedValue({
    profileId: PROFILE.id,
    items: [
      { permissionKey: "sales.view", isAllowed: true },
      { permissionKey: "sales.create", isAllowed: true },
      { permissionKey: "sales.update", isAllowed: false },
    ],
  });
  vi.mocked(membershipService.list).mockReset().mockResolvedValue([
    { companyUserId: "c1", identityUserId: "u1", username: "ana", fullName: "Ana", email: null, role: "User", isActive: true, profileId: PROFILE.id, profileName: PROFILE.name },
    { companyUserId: "c2", identityUserId: "u2", username: "adm", fullName: "Adm", email: null, role: "Admin", isActive: true, profileId: PROFILE.id, profileName: PROFILE.name },
  ]);
  vi.mocked(profileService.upsertPermissions).mockReset();
  vi.mocked(message.confirm).mockReset().mockResolvedValue(true);
  vi.mocked(message.success).mockReset();
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
    // IAM-V1-F2: la tabla lee los permisos de cada perfil solo para la columna «Concede»;
    // esta pantalla nunca los modifica.
    expect(profileService.upsertPermissions).not.toHaveBeenCalled();
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

  it("crear perfil muestra message.success al guardar", async () => {
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

    await waitFor(() => expect(message.success).toHaveBeenCalled());
  });

  it("si el backend falla al crear, no cierra el modal ni llama message.success", async () => {
    vi.mocked(profileService.create).mockRejectedValue({
      isAxiosError: true,
      response: { status: 500, data: {} },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Ventas Jr.")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: /Nuevo perfil/i }));
    await waitFor(() => screen.getByLabelText(/Nombre del perfil/i));

    fireEvent.change(screen.getByLabelText(/Nombre del perfil/i), {
      target: { value: "Nuevo" },
    });
    fireEvent.click(screen.getByRole("button", { name: /Guardar perfil/i }));

    await waitFor(() =>
      expect(screen.getByLabelText(/Nombre del perfil/i)).toBeTruthy(),
    );
    expect(message.success).not.toHaveBeenCalled();
  });
});

describe("ProfilesPage — IAM-V1-F2 función administrativa y alcance de cada perfil", () => {
  it("presenta «Administrador de empresa» como función fija, no como fila editable", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Ventas Jr.")).toBeTruthy());

    const card = screen.getByRole("note");
    expect(card.textContent).toContain("Administrador de empresa");
    expect(card.textContent).toContain("no como perfil");
    expect(screen.getAllByRole("row").some((r) => r.textContent?.includes("Administrador de empresa"))).toBe(false);
  });

  it("muestra cuántos permisos concede y cuántos usuarios operativos lo usan (sin contar administradores)", async () => {
    renderPage();
    expect(await screen.findByText("2 permisos")).toBeTruthy();
    const row = screen.getAllByRole("row").find((r) => r.textContent?.includes("Ventas Jr."))!;
    expect(row.textContent).toContain("2 permisos");
    const cells = within(row).getAllByRole("cell").map((c) => c.textContent);
    // La columna «Usuarios operativos» sigue a «Concede»: 1 (el Administrador no se cuenta).
    expect(cells[cells.indexOf("2 permisos") + 1]).toBe("1");
  });
});

describe("ProfilesPage — activar/desactivar con confirmación", () => {
  it("pide confirmación antes de desactivar y llama a update al confirmar", async () => {
    vi.mocked(profileService.update).mockResolvedValue({
      ...PROFILE,
      isActive: false,
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Ventas Jr.")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalled();
      expect(profileService.update).toHaveBeenCalledWith(
        expect.objectContaining({ isActive: false }),
      );
    });
    expect(message.success).toHaveBeenCalled();
  });

  it("si se cancela la confirmación, no llama a update", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderPage();
    await waitFor(() => expect(screen.getByText("Ventas Jr.")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(profileService.update).not.toHaveBeenCalled();
  });

  it("si el backend falla al desactivar, muestra el error real y no llama success", async () => {
    vi.mocked(profileService.update).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El perfil tiene usuarios activos." } },
      },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Ventas Jr.")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() =>
      expect(screen.getByText("El perfil tiene usuarios activos.")).toBeTruthy(),
    );
    expect(message.success).not.toHaveBeenCalled();
  });
});
