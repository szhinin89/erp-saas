// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  render,
  screen,
  waitFor,
  fireEvent,
  cleanup,
} from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { useAuthStore } from "../../../../store/authStore";
import { UsersPage } from "./UsersPage";
import { membershipService } from "../api/membershipService";
import { branchAssignmentService } from "../api/branchAssignmentService";
import { companyUserPreferencesService } from "../../api/companyUserPreferencesService";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { message } from "../../../../lib/messages";

const navigateMock = vi.fn();

vi.mock("react-router-dom", async () => {
  const actual =
    await vi.importActual<typeof import("react-router-dom")>(
      "react-router-dom",
    );
  return { ...actual, useNavigate: () => navigateMock };
});

vi.mock("../api/membershipService", () => ({
  MEMBERSHIP_ROLES: ["Admin", "User"],
  membershipService: {
    list: vi.fn(),
    upsertMembership: vi.fn(),
    revokeMembership: vi.fn(),
    createSystemUser: vi.fn(),
    lookupUserByUsername: vi.fn(),
  },
}));

vi.mock("../api/branchAssignmentService", () => ({
  branchAssignmentService: {
    getMembershipBranches: vi.fn(),
    updateMembershipBranches: vi.fn(),
  },
}));

vi.mock("../../api/companyUserPreferencesService", () => ({
  COMPANY_USER_LOGIN_MODES: ["AskBranch", "DirectToDefault"],
  companyUserPreferencesService: {
    get: vi.fn(),
    update: vi.fn(),
  },
}));

vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const membershipRow = {
  companyUserId: "membership-1",
  identityUserId: "identity-1",
  username: "ana",
  fullName: "Ana Perez",
  email: "ana@test.com",
  role: "User",
  isActive: true,
  profileId: "profile-1",
  profileName: "Ventas",
};

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <UsersPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

function setUserRole(role: string | null) {
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
  vi.mocked(membershipService.list).mockResolvedValue([membershipRow]);
  vi.mocked(membershipService.upsertMembership).mockReset();
  vi.mocked(membershipService.revokeMembership).mockReset();
  vi.mocked(branchAssignmentService.getMembershipBranches).mockResolvedValue({
    companyUserId: membershipRow.companyUserId,
    branches: [
      { branchId: "branch-1", branchName: "Matriz", authorized: true },
      { branchId: "branch-2", branchName: "Sucursal Norte", authorized: false },
    ],
  });
  vi.mocked(companyUserPreferencesService.get).mockResolvedValue({
    companyUserId: membershipRow.companyUserId,
    defaultBranchId: null,
    loginMode: "AskBranch",
  });
  vi.mocked(message.confirm).mockResolvedValue(true);
  setUserRole("Admin");
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

describe("UsersPage — access gate", () => {
  it("sin permiso, no admin: renderiza NoAccessPage y no ve la tabla ni acciones", async () => {
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: () => false,
      has: () => false,
      isAdminRole: false,
    });

    renderPage();

    expect(
      screen.getAllByText("No tienes acceso a esta pantalla.").length,
    ).toBeGreaterThan(0);
    expect(
      screen.queryByRole("button", { name: /Agregar usuario/ }),
    ).toBeNull();
    expect(membershipService.list).not.toHaveBeenCalled();
  });

  it("con permiso: renderiza la pantalla (no NoAccessPage)", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Ana Perez")).toBeTruthy());
    expect(screen.queryByText("No tienes acceso a esta pantalla.")).toBeNull();
  });
});

describe("UsersPage — tabla principal", () => {
  it("carga memberships y muestra los datos de la fila", async () => {
    renderPage();

    await waitFor(() => {
      expect(membershipService.list).toHaveBeenCalledWith(false);
      expect(screen.getByText("Ana Perez")).toBeTruthy();
      expect(screen.getByText("ana@test.com")).toBeTruthy();
      expect(screen.getByText("Ventas")).toBeTruthy();
    });
  });

  it("un error al cargar la lista se muestra con la infraestructura existente", async () => {
    vi.mocked(membershipService.list).mockRejectedValue({
      isAxiosError: true,
      response: { status: 500, data: {} },
    });

    renderPage();

    await waitFor(() =>
      expect(
        screen.getByText("No se pudo cargar la lista de usuarios."),
      ).toBeTruthy(),
    );
  });
});

describe("UsersPage — navegación a la pantalla única de configuración", () => {
  it('el botón "Agregar usuario" navega a /access/users/new preservando el filtro activo en `from`', async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Ana Perez")).toBeTruthy());

    fireEvent.change(screen.getByPlaceholderText("Código, nombre o texto…"), {
      target: { value: "ana" },
    });
    fireEvent.click(screen.getByRole("button", { name: /Agregar usuario/ }));

    expect(navigateMock).toHaveBeenCalledWith("/access/users/new", {
      state: { from: "/access/users?q=ana" },
    });
  });

  it('click en una fila (o en "Configurar") navega a /access/users/:companyUserId con `from` para volver a la lista', async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Ana Perez")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Configurar" }));

    expect(navigateMock).toHaveBeenCalledWith("/access/users/membership-1", {
      state: { from: "/access/users" },
    });
  });
});

describe("UsersPage — revocación", () => {
  it("pide confirmación antes de revocar y llama revokeMembership al confirmar", async () => {
    vi.mocked(membershipService.revokeMembership).mockResolvedValue({});

    renderPage();
    await waitFor(() => expect(screen.getByText("Ana Perez")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Revocar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalled();
      expect(membershipService.revokeMembership).toHaveBeenCalledWith("ana");
    });
  });

  it("no llama revokeMembership si el usuario cancela la confirmación", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderPage();
    await waitFor(() => expect(screen.getByText("Ana Perez")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Revocar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(membershipService.revokeMembership).not.toHaveBeenCalled();
  });
});

describe("UsersPage — reactivación", () => {
  it("pide confirmación antes de reactivar y llama upsertMembership al confirmar", async () => {
    vi.mocked(membershipService.list).mockResolvedValue([
      { ...membershipRow, isActive: false },
    ]);
    vi.mocked(membershipService.upsertMembership).mockResolvedValue({});

    renderPage();
    await waitFor(() => expect(screen.getByText("Ana Perez")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Reactivar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalled();
      expect(membershipService.upsertMembership).toHaveBeenCalledWith({
        username: "ana",
        role: "User",
        profileId: "profile-1",
      });
    });
    expect(message.success).toHaveBeenCalled();
  });

  it("no llama upsertMembership si el usuario cancela la confirmación", async () => {
    vi.mocked(membershipService.list).mockResolvedValue([
      { ...membershipRow, isActive: false },
    ]);
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderPage();
    await waitFor(() => expect(screen.getByText("Ana Perez")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Reactivar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(membershipService.upsertMembership).not.toHaveBeenCalled();
  });
});

describe("UsersPage — permisos", () => {
  it("usuario sin permiso no ve acciones de la tabla", async () => {
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: () => false,
      has: () => false,
      isAdminRole: true,
    });

    renderPage();

    await waitFor(() => expect(screen.getByText("Ana Perez")).toBeTruthy());
    expect(screen.queryByRole("button", { name: "Configurar" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Revocar" })).toBeNull();
    expect(
      screen.queryByRole("button", { name: /Agregar usuario/ }),
    ).toBeNull();
  });
});
