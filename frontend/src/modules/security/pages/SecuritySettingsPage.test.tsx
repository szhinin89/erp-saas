// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, cleanup, fireEvent } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { SecuritySettingsPage } from "./SecuritySettingsPage";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { securityService } from "../api/securityService";
import { message } from "../../../lib/messages";

vi.mock("../api/securityService", () => ({
  securityService: {
    getAdminMatrix: vi.fn(),
    upsertAdminScopes: vi.fn(),
  },
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

const MATRIX_WITH_USER = {
  users: [
    {
      id: "user-1",
      companyUserMembershipId: "membership-1",
      fullName: "Ana Perez",
      username: "ana",
      email: "ana@test.com",
      role: "User",
      isActive: true,
    },
  ],
  assignments: [],
};

beforeEach(() => {
  vi.mocked(securityService.getAdminMatrix).mockResolvedValue({
    users: [],
    assignments: [],
  });
  vi.mocked(securityService.upsertAdminScopes).mockReset();
  vi.mocked(message.confirm).mockReset().mockResolvedValue(true);
  vi.mocked(message.success).mockReset();
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

/**
 * ADMIN-SECURITY-SPLIT-01 (commit 363f371f) reemplazó el gate de esta pantalla
 * de rol literal (`role === "Admin"`) a permiso real
 * (`canShow("admin.delegation.view")`) — el componente ya ni siquiera lee
 * `authStore`. `canView` mockeado aquí es la única variable que controla
 * `NoAccessPage` vs. contenido; ya no hay `role` que simular.
 */
function mockCanView(canView: boolean) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => canView,
    has: () => canView,
    isAdminRole: false,
  });
}

describe("SecuritySettingsPage — access gate", () => {
  const EMPTY_USERS_MESSAGE =
    "No hay usuarios en la matriz de delegación. Si acaba de crear la empresa, cree al menos un usuario administrador.";
  const NO_ACCESS_MESSAGE = "No tienes acceso a esta pantalla.";

  it("con permiso admin.delegation.view renderiza el contenido (no NoAccessPage)", async () => {
    mockCanView(true);
    renderPage();

    await waitFor(() => {
      expect(screen.getByText(EMPTY_USERS_MESSAGE)).toBeTruthy();
    });
    expect(screen.queryByText(NO_ACCESS_MESSAGE)).toBeNull();
  });

  it("sin permiso admin.delegation.view renderiza NoAccessPage", () => {
    mockCanView(false);
    renderPage();

    expect(screen.getAllByText(NO_ACCESS_MESSAGE).length).toBeGreaterThan(0);
    expect(screen.queryByText(EMPTY_USERS_MESSAGE)).toBeNull();
  });
});

describe("SecuritySettingsPage — confirmación y feedback por toggle", () => {
  function mockCanView(canView: boolean, canConfigure = canView) {
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: (perm: string) =>
        perm === "admin.delegation.configure" ? canConfigure : canView,
      has: () => canView,
      isAdminRole: false,
    });
  }

  it("pide confirmación antes de otorgar una capacidad y llama a upsertAdminScopes al confirmar", async () => {
    mockCanView(true);
    vi.mocked(securityService.getAdminMatrix).mockResolvedValue(
      MATRIX_WITH_USER,
    );
    vi.mocked(securityService.upsertAdminScopes).mockResolvedValue({});

    render(
      <I18nProvider>
        <SecuritySettingsPage />
      </I18nProvider>,
    );

    await waitFor(() => expect(screen.getAllByText("Ana Perez").length).toBeGreaterThan(0));

    const toggles = screen.getAllByRole("switch");
    fireEvent.click(toggles[0]);

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalled();
      expect(securityService.upsertAdminScopes).toHaveBeenCalledWith({
        subjectType: "User",
        subjectKey: "user-1",
        allowedScopes: [1],
      });
    });
    expect(message.success).toHaveBeenCalled();
  });

  it("si se cancela la confirmación, no llama a upsertAdminScopes", async () => {
    mockCanView(true);
    vi.mocked(securityService.getAdminMatrix).mockResolvedValue(
      MATRIX_WITH_USER,
    );
    vi.mocked(message.confirm).mockResolvedValue(false);

    render(
      <I18nProvider>
        <SecuritySettingsPage />
      </I18nProvider>,
    );

    await waitFor(() => expect(screen.getAllByText("Ana Perez").length).toBeGreaterThan(0));

    const toggles = screen.getAllByRole("switch");
    fireEvent.click(toggles[0]);

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(securityService.upsertAdminScopes).not.toHaveBeenCalled();
    // El toggle nunca se movió — sin estado optimista falso.
    expect(toggles[0].getAttribute("aria-checked")).toBe("false");
  });

  it("si falla el backend, muestra el error real y conserva el estado anterior del toggle", async () => {
    mockCanView(true);
    vi.mocked(securityService.getAdminMatrix).mockResolvedValue(
      MATRIX_WITH_USER,
    );
    vi.mocked(securityService.upsertAdminScopes).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 500,
        data: { message: { user: "No se pudo actualizar la capacidad." } },
      },
    });

    render(
      <I18nProvider>
        <SecuritySettingsPage />
      </I18nProvider>,
    );

    await waitFor(() => expect(screen.getAllByText("Ana Perez").length).toBeGreaterThan(0));

    const toggles = screen.getAllByRole("switch");
    fireEvent.click(toggles[0]);

    await waitFor(() =>
      expect(
        screen.getByText("No se pudo actualizar la capacidad."),
      ).toBeTruthy(),
    );
    expect(message.success).not.toHaveBeenCalled();
    const togglesAfter = screen.getAllByRole("switch");
    expect(togglesAfter[0].getAttribute("aria-checked")).toBe("false");
  });
});

// NOTA (SECURITY-SETTINGS-TEST-FIX-01, causa raíz): el describe
// "SecuritySettingsPage — preferences (Fase G)" que vivía aquí probaba un
// modal de Preferencias (login-mode/default-branch) que el commit 363f371f
// ("Rename Seguridad to Delegación de administración and fix its
// authorization") eliminó deliberadamente de este componente — mutaba el
// mismo registro que la pestaña "Preferencias de acceso" de Acceso usuarios
// mediante dos formularios independientes y descoordinados. Esos tests
// quedaron huérfanos (SecuritySettingsPage.tsx ya no importa
// companyUserPreferencesFacade/branchLookupFacade ni renderiza ningún botón
// "Preferencias"), no un bug: la cobertura real de esa edición ya vive en
// UserConfigPage.test.tsx ("Preferencias de acceso" — carga, actualiza,
// deshabilita, error 422), verificado antes de borrar este bloque.
