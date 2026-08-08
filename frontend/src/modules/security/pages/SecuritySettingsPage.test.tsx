// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  render,
  screen,
  waitFor,
  fireEvent,
  cleanup,
} from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { SecuritySettingsPage } from "./SecuritySettingsPage";
import { companyUserPreferencesFacade } from "../../access/facades/companyUserPreferencesFacade";
import { branchLookupFacade } from "../../branches/facades/branchLookupFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";

vi.mock("../api/securityService", () => ({
  securityService: {
    getAdminMatrix: vi.fn().mockResolvedValue({ users: [], assignments: [] }),
    upsertAdminScopes: vi.fn(),
  },
}));

vi.mock("../../access/facades/companyUserPreferencesFacade", () => ({
  COMPANY_USER_LOGIN_MODES: ["AskBranch", "DirectToDefault"],
  companyUserPreferencesFacade: {
    get: vi.fn(),
    update: vi.fn(),
  },
}));

vi.mock("../../branches/facades/branchLookupFacade", () => ({
  branchLookupFacade: {
    list: vi.fn().mockResolvedValue([]),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

beforeEach(() => {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
});

afterEach(() => {
  cleanup();
});

function renderPage() {
  return render(
    <I18nProvider>
      <SecuritySettingsPage />
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

describe("SecuritySettingsPage — access gate", () => {
  afterEach(() => {
    useAuthStore.setState({
      user: null,
      isAuthenticated: false,
      hasHydrated: false,
      token: null,
      companySessionVersion: 0,
    });
  });

  const EMPTY_USERS_MESSAGE =
    "No hay usuarios en la matriz de delegación. Si acaba de crear la empresa, cree al menos un usuario administrador.";
  const NO_ACCESS_MESSAGE = "No tienes acceso a esta pantalla.";

  it("Admin role renders the page content (not NoAccessPage)", async () => {
    setUserRole("Admin");
    renderPage();

    await waitFor(() => {
      expect(screen.getByText(EMPTY_USERS_MESSAGE)).toBeTruthy();
    });
    expect(screen.queryByText(NO_ACCESS_MESSAGE)).toBeNull();
  });

  it("non-admin role renders NoAccessPage", () => {
    setUserRole("User");
    renderPage();

    expect(screen.getAllByText(NO_ACCESS_MESSAGE).length).toBeGreaterThan(0);
    expect(screen.queryByText(EMPTY_USERS_MESSAGE)).toBeNull();
  });

  it("no session (deny by default) renders NoAccessPage", () => {
    setUserRole(null);
    renderPage();

    expect(screen.getAllByText(NO_ACCESS_MESSAGE).length).toBeGreaterThan(0);
  });
});

describe("SecuritySettingsPage — preferences (Fase G)", () => {
  const membershipId = "membership-1";
  const matrixWithOneUser = {
    users: [
      {
        id: "identity-1",
        companyUserMembershipId: membershipId,
        fullName: "Ana Perez",
        username: "ana",
        email: "ana@test.com",
        role: "User",
        isActive: true,
      },
    ],
    assignments: [],
  };

  async function openPreferencesModalFor(fullName: string) {
    await waitFor(() =>
      expect(
        screen.getByText(fullName, { selector: ".userName" }),
      ).toBeTruthy(),
    );
    fireEvent.click(screen.getByRole("button", { name: "Preferencias" }));
    await waitFor(() => expect(screen.getByRole("dialog")).toBeTruthy());
  }

  beforeEach(async () => {
    setUserRole("Admin");
    const { securityService } = await import("../api/securityService");
    vi.mocked(securityService.getAdminMatrix).mockResolvedValue(
      matrixWithOneUser,
    );
    vi.mocked(companyUserPreferencesFacade.get).mockReset();
    vi.mocked(companyUserPreferencesFacade.update).mockReset();
    vi.mocked(branchLookupFacade.list).mockResolvedValue([
      {
        id: "branch-1",
        name: "Matriz",
        code: "001",
        address: "",
        countryId: null,
        provinceId: null,
        cantonId: null,
        parishId: null,
        phone: null,
        email: null,
        managerName: null,
        isActive: true,
        isMainBranch: true,
      },
      {
        id: "branch-2",
        name: "Sucursal Norte",
        code: "002",
        address: "",
        countryId: null,
        provinceId: null,
        cantonId: null,
        parishId: null,
        phone: null,
        email: null,
        managerName: null,
        isActive: true,
        isMainBranch: false,
      },
    ]);
  });

  afterEach(() => {
    useAuthStore.setState({
      user: null,
      isAuthenticated: false,
      hasHydrated: false,
      token: null,
      companySessionVersion: 0,
    });
  });

  it("carga las preferencias existentes al abrir el modal usando el companyUserMembershipId", async () => {
    vi.mocked(companyUserPreferencesFacade.get).mockResolvedValue({
      companyUserId: membershipId,
      defaultBranchId: "branch-2",
      loginMode: "DirectToDefault",
    });

    renderPage();
    await openPreferencesModalFor("Ana Perez");

    expect(companyUserPreferencesFacade.get).toHaveBeenCalledWith(
      membershipId,
    );
  });

  it("muestra los valores existentes en el formulario", async () => {
    vi.mocked(companyUserPreferencesFacade.get).mockResolvedValue({
      companyUserId: membershipId,
      defaultBranchId: "branch-2",
      loginMode: "DirectToDefault",
    });

    renderPage();
    await openPreferencesModalFor("Ana Perez");

    await waitFor(() => {
      const loginModeSelect = screen.getByLabelText(
        "Modo de ingreso",
      ) as HTMLSelectElement;
      expect(loginModeSelect.value).toBe("DirectToDefault");
      const branchSelect = screen.getByLabelText(
        "Sucursal por defecto",
      ) as HTMLSelectElement;
      expect(branchSelect.value).toBe("branch-2");
    });
  });

  it("el update envía companyUserId por la ruta (no en el body) y solo los campos editables", async () => {
    vi.mocked(companyUserPreferencesFacade.get).mockResolvedValue({
      companyUserId: membershipId,
      defaultBranchId: null,
      loginMode: "AskBranch",
    });
    vi.mocked(companyUserPreferencesFacade.update).mockResolvedValue({
      companyUserId: membershipId,
      defaultBranchId: "branch-1",
      loginMode: "DirectToDefault",
    });

    renderPage();
    await openPreferencesModalFor("Ana Perez");

    fireEvent.change(screen.getByLabelText("Modo de ingreso"), {
      target: { value: "DirectToDefault" },
    });
    fireEvent.change(screen.getByLabelText("Sucursal por defecto"), {
      target: { value: "branch-1" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() => {
      expect(companyUserPreferencesFacade.update).toHaveBeenCalledWith(
        membershipId,
        {
          loginMode: "DirectToDefault",
          defaultBranchId: "branch-1",
        },
      );
    });
  });

  it("un error 422 del backend muestra el mensaje usando la infraestructura existente", async () => {
    vi.mocked(companyUserPreferencesFacade.get).mockResolvedValue({
      companyUserId: membershipId,
      defaultBranchId: null,
      loginMode: "AskBranch",
    });
    vi.mocked(companyUserPreferencesFacade.update).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 422,
        data: {
          data: {
            errors: [
              "La sucursal por defecto debe estar previamente autorizada para este usuario.",
            ],
          },
        },
      },
    });

    renderPage();
    await openPreferencesModalFor("Ana Perez");

    fireEvent.change(screen.getByLabelText("Modo de ingreso"), {
      target: { value: "DirectToDefault" },
    });
    fireEvent.change(screen.getByLabelText("Sucursal por defecto"), {
      target: { value: "branch-1" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() => {
      expect(
        screen.getByText(
          "La sucursal por defecto debe estar previamente autorizada para este usuario.",
        ),
      ).toBeTruthy();
    });
  });

  it("usuario sin permiso access.company_user_memberships.view no ve la acción de preferencias", async () => {
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: () => false,
      has: () => false,
      isAdminRole: true,
    });

    renderPage();

    await waitFor(() =>
      expect(
        screen.getByText("Ana Perez", { selector: ".userName" }),
      ).toBeTruthy(),
    );
    expect(screen.queryByRole("button", { name: "Preferencias" })).toBeNull();
  });
});
