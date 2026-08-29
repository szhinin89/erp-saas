// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { PermissionsAssignmentPage } from "./PermissionsAssignmentPage";
import { profileService } from "../api/profileService";
import { adminPermissionsService } from "../api/adminPermissionsService";
import { message } from "../../../lib/messages";

/**
 * ADMIN-PERMISSIONS-SSOT-KERNEL-02: el árbol de grupos/pantallas/acciones ahora se carga desde
 * adminPermissionsService.getCatalog() (backend, derivado de KernelRegistry) — no hay
 * MODULE_PERM_GROUPS hardcodeado en el componente. Estos tests fijan esa fuente única.
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

vi.mock("../api/adminPermissionsService", () => ({
  adminPermissionsService: {
    getCatalog: vi.fn(),
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

const PROFILE = {
  id: "profile-1",
  name: "Ventas Jr.",
  description: "Perfil de ventas junior",
  isActive: true,
};

// Catálogo real de una sola pantalla (Pagos a proveedores) — mismo ejemplo literal usado en el
// backend para probar view + acciones relacionadas (create/reverse).
const CATALOG = {
  groups: [
    {
      code: "suppliers",
      labelKey: "app.nav.group.suppliers",
      sortOrder: 12,
      categories: [
        {
          id: "category-payables",
          labelKey: "app.nav.item.suppliers.payablesGroup",
          sortOrder: 30,
          items: [
            {
              id: "item-supplier-payments",
              labelKey: "app.nav.item.payables.supplierPayments",
              route: "/supplier-payments",
              permission: "supplier-payments.view",
              sortOrder: 40,
              actions: [
                {
                  code: "supplier-payments.view",
                  label: "Ver / Acceder",
                  description: "Permite ver y acceder a esta pantalla.",
                  sortOrder: 0,
                },
                {
                  code: "supplier-payments.create",
                  label: "Crear",
                  description: "Permite crear nuevos registros.",
                  sortOrder: 1,
                },
                {
                  code: "supplier-payments.reverse",
                  label: "Reversar",
                  description: "Permite reversar la operación.",
                  sortOrder: 2,
                },
              ],
            },
          ],
        },
      ],
    },
  ],
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
    items: [{ permissionKey: "supplier-payments.view", isAllowed: true }],
  });
  vi.mocked(profileService.upsertPermissions).mockReset();
  vi.mocked(profileService.upsertPermissions).mockResolvedValue({
    saved: [],
    rejected: [],
    allSaved: true,
  });
  vi.mocked(profileService.create).mockReset();
  vi.mocked(profileService.update).mockReset();
  vi.mocked(adminPermissionsService.getCatalog).mockResolvedValue(CATALOG);
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

describe("PermissionsAssignmentPage — catálogo dinámico desde el backend", () => {
  it("carga el catálogo y el listado de perfiles al montar", async () => {
    renderPage();

    await waitFor(() => {
      expect(adminPermissionsService.getCatalog).toHaveBeenCalled();
      expect(profileService.list).toHaveBeenCalled();
    });
  });

  it("sin perfil seleccionado, no carga permisos ni muestra el árbol", async () => {
    renderPage();

    await waitFor(() => expect(profileService.list).toHaveBeenCalled());

    expect(profileService.getPermissions).not.toHaveBeenCalled();
    expect(screen.queryByText("Pagos a proveedores")).toBeNull();
  });

  it("preseleccionando ?profileId= carga permisos y renderiza grupo/pantalla/acciones desde el catálogo", async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => {
      expect(profileService.getPermissions).toHaveBeenCalledWith(PROFILE.id);
    });

    expect(await screen.findByText("Proveedores")).toBeTruthy();
    expect(await screen.findByText("Pagos a proveedores")).toBeTruthy();
    expect(screen.getByText("Ver / Acceder")).toBeTruthy();
    expect(screen.getByText("Crear")).toBeTruthy();
    expect(screen.getByText("Reversar")).toBeTruthy();
  });

  it("no queda ninguna referencia a los módulos hardcodeados de MODULE_PERM_GROUPS", async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => expect(screen.getByText("Pagos a proveedores")).toBeTruthy());

    expect(screen.queryByText("Clientes / Proveedores")).toBeNull();
    expect(screen.queryByText("RIDE (Ventas)")).toBeNull();
    expect(screen.queryByText("Facturación Electrónica")).toBeNull();
  });

  it("togglear una acción y guardar pide confirmación y llama a upsertPermissions solo con códigos del catálogo", async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => expect(screen.getByText("Pagos a proveedores")).toBeTruthy());

    const switches = screen.getAllByRole("switch");
    fireEvent.click(switches[1]); // "Crear"

    fireEvent.click(screen.getByRole("button", { name: /Guardar permisos/i }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalled();
      expect(profileService.upsertPermissions).toHaveBeenCalled();
    });
    const [, sentItems] = vi.mocked(profileService.upsertPermissions).mock.calls[0];
    const catalogCodes = new Set([
      "supplier-payments.view",
      "supplier-payments.create",
      "supplier-payments.reverse",
    ]);
    for (const item of sentItems) expect(catalogCodes.has(item.permissionKey)).toBe(true);
  });

  it("si se cancela la confirmación, no llama a upsertPermissions", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => expect(screen.getByText("Pagos a proveedores")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: /Guardar permisos/i }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(profileService.upsertPermissions).not.toHaveBeenCalled();
  });

  it("al guardar exitosamente muestra message.success", async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => expect(screen.getByText("Pagos a proveedores")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: /Guardar permisos/i }));

    await waitFor(() => expect(message.success).toHaveBeenCalled());
  });

  it("si el backend falla, muestra el mensaje de error real y no llama message.success", async () => {
    vi.mocked(profileService.upsertPermissions).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 422,
        data: { data: { errors: ["El permiso ya no existe en el catálogo."] } },
      },
    });
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => expect(screen.getByText("Pagos a proveedores")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: /Guardar permisos/i }));

    await waitFor(() =>
      expect(screen.getByText("El permiso ya no existe en el catálogo.")).toBeTruthy(),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("el filtro de texto oculta pantallas que no coinciden", async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => expect(screen.getByText("Pagos a proveedores")).toBeTruthy());

    fireEvent.change(screen.getByPlaceholderText(/Buscar módulo, categoría, pantalla/i), {
      target: { value: "no-existe-esta-pantalla" },
    });

    expect(screen.queryByText("Pagos a proveedores")).toBeNull();
    expect(screen.getByText(/Ningún grupo o pantalla coincide/i)).toBeTruthy();
  });

  it("no expone ningún campo de creación de usuario o perfil", async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => expect(screen.getByText("Pagos a proveedores")).toBeTruthy());

    expect(screen.queryByLabelText(/Nombre del perfil/i)).toBeNull();
    expect(screen.queryByText(/Nuevo perfil/i)).toBeNull();
    expect(profileService.create).not.toHaveBeenCalled();
  });
});

describe("PermissionsAssignmentPage — refactor visual compacto (PERMISSIONS-ASSIGNMENT-UI-COMPACT-03)", () => {
  it('"Marcar todo" activa las 3 acciones y "Desmarcar todo" las apaga, sin llamar al backend', async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => expect(screen.getByText("Pagos a proveedores")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Marcar todo" }));
    let switches = screen.getAllByRole("switch");
    expect(switches.every((s) => s.getAttribute("aria-checked") === "true")).toBe(true);

    fireEvent.click(screen.getByRole("button", { name: "Desmarcar todo" }));
    switches = screen.getAllByRole("switch");
    expect(switches.every((s) => s.getAttribute("aria-checked") === "false")).toBe(true);

    expect(profileService.upsertPermissions).not.toHaveBeenCalled();
  });

  it('"Solo permisos de acceso (Ver)" activa únicamente la primera acción de cada pantalla', async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => expect(screen.getByText("Pagos a proveedores")).toBeTruthy());

    fireEvent.click(
      screen.getByRole("button", { name: "Solo permisos de acceso (Ver)" }),
    );

    const switches = screen.getAllByRole("switch");
    expect(switches[0].getAttribute("aria-checked")).toBe("true");
    expect(switches.slice(1).every((s) => s.getAttribute("aria-checked") === "false")).toBe(true);
  });

  it("el chevron contrae y expande el bloque de la pantalla sin perder el estado de los toggles", async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => expect(screen.getByText("Pagos a proveedores")).toBeTruthy());
    expect(screen.getByText("Crear")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: /Contraer Pagos a proveedores/i }));
    expect(screen.queryByText("Crear")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: /Expandir Pagos a proveedores/i }));
    expect(screen.getByText("Crear")).toBeTruthy();
  });

  it("muestra el resumen de pantallas/permisos totales y el conteo por bloque", async () => {
    renderPage(`/admin/permissions?profileId=${PROFILE.id}`);

    await waitFor(() => expect(screen.getByText("Pagos a proveedores")).toBeTruthy());

    expect(screen.getByText("Total: 1 pantallas, 3 permisos")).toBeTruthy();
    expect(screen.getByText("Acceso: 1")).toBeTruthy();
    expect(screen.getByText("Acciones: 2")).toBeTruthy();
    expect(screen.getByText("3 permisos")).toBeTruthy();
  });
});
