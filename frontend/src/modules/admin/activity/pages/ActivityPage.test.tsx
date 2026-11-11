// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { useAuthStore } from "../../../../store/authStore";
import { useSessionStore } from "../../../../store/sessionStore";
import { ActivityPage } from "./ActivityPage";

const my = vi.fn();

vi.mock("../../api/activityService", () => ({
  activityService: { my: (...args: unknown[]) => my(...args) },
}));

function row(overrides: Record<string, unknown> = {}) {
  return {
    id: "a-1",
    module: "branches",
    action: "branch.create",
    entityType: "Branch",
    entityId: "b-1",
    description: "Sucursal Matriz",
    createdAt: "2026-07-01T10:00:00Z",
    userEmail: "admin@test.com",
    userFullName: "Admin",
    ...overrides,
  };
}

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: {
      userId: "u-1",
      fullName: "Admin",
      username: "admin",
      email: "admin@test.com",
      role: "User",
      tenantId: "tenant-1",
    },
    isAuthenticated: true,
    hasHydrated: true,
  });
  useSessionStore.setState({
    identity: null,
    tenant: null,
    preferences: null,
    isLoaded: true,
    authorization: { roles: [], permissions },
  });
}

function renderPage() {
  return render(
    <I18nProvider>
      <ActivityPage />
    </I18nProvider>,
  );
}

describe("ActivityPage", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    useAuthStore.setState({
      user: null,
      isAuthenticated: false,
      hasHydrated: false,
      token: null,
      companySessionVersion: 0,
    });
    useSessionStore.setState({
      identity: null,
      tenant: null,
      authorization: null,
      preferences: null,
      isLoaded: false,
    });
  });

  it("sin permiso admin.activity.view muestra NoAccessPage y no consulta", () => {
    setPermissions(["other.permission"]);
    renderPage();
    expect(
      screen.getAllByText("No tienes acceso a esta pantalla.").length,
    ).toBeGreaterThan(0);
    expect(my).not.toHaveBeenCalled();
  });

  it("aclara que es la actividad del usuario autenticado y no un feed global", async () => {
    setPermissions(["admin.activity.view"]);
    my.mockResolvedValue([]);
    renderPage();
    await waitFor(() => expect(my).toHaveBeenCalled());
    expect(screen.getByText(/únicamente su actividad personal/)).toBeTruthy();
    expect(screen.queryByText("Usuario")).toBeNull();
  });

  it("traduce acción y módulo a etiquetas legibles y conserva el detalle", async () => {
    setPermissions(["admin.activity.view"]);
    my.mockResolvedValue([
      row(),
      row({
        id: "a-2",
        module: "companies",
        action: "company.admin.password-regenerate",
        description: "Contraseña temporal regenerada para jperez",
        entityType: "IdentityUser",
      }),
      row({ id: "a-3", module: "otro", action: "algo.update", description: null, entityType: null }),
    ]);

    renderPage();

    await waitFor(() => expect(screen.getByText("Sucursal creada")).toBeTruthy());
    // Módulo: opción del filtro + badge de la fila.
    expect(screen.getAllByText("Sucursales")).toHaveLength(2);
    expect(screen.getByText("Sucursal Matriz")).toBeTruthy();
    expect(screen.getByText("Contraseña temporal regenerada")).toBeTruthy();
    expect(screen.getAllByText("Empresas")).toHaveLength(2);
    // Acción y módulo no catalogados: verbo genérico y código tal cual.
    expect(screen.getByText("Actualización")).toBeTruthy();
    expect(screen.getByText("otro")).toBeTruthy();
  });

  it("el filtro de módulo es una lista cerrada, reinicia la página y se puede limpiar", async () => {
    setPermissions(["admin.activity.view"]);
    my.mockResolvedValue([]);
    renderPage();
    await waitFor(() => expect(my).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByDisplayValue("Todos los módulos"), {
      target: { value: "inventory" },
    });
    await waitFor(() =>
      expect(my).toHaveBeenLastCalledWith(
        expect.objectContaining({ module: "inventory", page: 1 }),
      ),
    );

    fireEvent.click(screen.getByText("Limpiar filtros"));
    await waitFor(() =>
      expect(my).toHaveBeenLastCalledWith(
        expect.objectContaining({ module: undefined, page: 1 }),
      ),
    );
  });

  it("pagina de 25 en 25 con el endpoint 'my' y muestra el estado vacío", async () => {
    setPermissions(["admin.activity.view"]);
    my.mockResolvedValue([]);
    renderPage();
    await waitFor(() =>
      expect(my).toHaveBeenCalledWith({ module: undefined, page: 1, pageSize: 25 }),
    );
    expect(screen.getByText(/no tiene actividad registrada/i)).toBeTruthy();
  });

  it("muestra el error del servicio", async () => {
    setPermissions(["admin.activity.view"]);
    my.mockRejectedValue(new Error("boom"));
    renderPage();
    await waitFor(() => expect(screen.getAllByText(/Error/).length).toBeGreaterThan(0));
  });
});
