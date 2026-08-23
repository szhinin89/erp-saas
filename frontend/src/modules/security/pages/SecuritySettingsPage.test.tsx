// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, cleanup } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { SecuritySettingsPage } from "./SecuritySettingsPage";
import { usePermissionsUi } from "../../../access/usePermissionsUi";

vi.mock("../api/securityService", () => ({
  securityService: {
    getAdminMatrix: vi.fn().mockResolvedValue({ users: [], assignments: [] }),
    upsertAdminScopes: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

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
