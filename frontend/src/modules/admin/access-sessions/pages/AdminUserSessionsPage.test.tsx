// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  render,
  screen,
  waitFor,
  fireEvent,
} from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { useAuthStore } from "../../../../store/authStore";
import { useSessionStore } from "../../../../store/sessionStore";
import { AdminUserSessionsPage } from "./AdminUserSessionsPage";

const getPaged = vi.fn();
const getStatistics = vi.fn();
const close = vi.fn();
const listMembers = vi.fn((..._args: unknown[]) => Promise.resolve<unknown[]>([]));

vi.mock("../../../access/facades/membershipLookupFacade", () => ({
  membershipLookupFacade: {
    list: (...args: unknown[]) => listMembers(...args),
  },
}));

vi.mock("../../api/userSessionAdminService", () => ({
  userSessionAdminService: {
    getPaged: (...args: unknown[]) => getPaged(...args),
    getStatistics: (...args: unknown[]) => getStatistics(...args),
    close: (...args: unknown[]) => close(...args),
  },
}));

const ROW = {
  sessionId: "session-1",
  identityUserId: "user-11111111-aaaa-bbbb-cccc-dddddddddddd",
  companyId: "company-11111111-aaaa-bbbb-cccc-dddddddddddd",
  branchId: "branch-11111111-aaaa-bbbb-cccc-dddddddddddd",
  terminalId: "device-1",
  status: "Active" as const,
  createdAt: "2026-07-01T10:00:00Z",
  updatedAt: null,
  userFullName: "Juan Pérez",
  username: "jperez",
  branchName: "Matriz",
};

const STATS = {
  active: 1,
  closedManually: 2,
  closedByNewLogin: 3,
  expired: 4,
  total: 10,
};

function renderPage() {
  return render(
    <I18nProvider>
      <AdminUserSessionsPage />
    </I18nProvider>,
  );
}

function setAuth(role: string | null) {
  useAuthStore.setState({
    user:
      role === null
        ? null
        : {
            userId: "admin-1",
            fullName: "Admin",
            username: "admin",
            email: "admin@test.com",
            role,
            tenantId: "tenant-1",
          },
    isAuthenticated: role !== null,
    hasHydrated: true,
  });
}

function setPermissions(permissions: string[]) {
  useSessionStore.setState({
    identity: null,
    tenant: null,
    preferences: null,
    isLoaded: true,
    authorization: { roles: [], permissions },
  });
}

describe("AdminUserSessionsPage", () => {
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

  it("sin permiso access.sessions.view muestra NoAccessPage y no llama al servicio", () => {
    setAuth("User");
    setPermissions(["other.permission"]);

    renderPage();

    expect(
      screen.getAllByText("No tienes acceso a esta pantalla.").length,
    ).toBeGreaterThan(0);
    expect(getPaged).not.toHaveBeenCalled();
  });

  it("con permiso, carga y muestra la tabla y las estadísticas", async () => {
    setAuth("User");
    setPermissions(["access.sessions.view", "access.sessions.close"]);
    getPaged.mockResolvedValue({
      items: [ROW],
      pageNumber: 1,
      pageSize: 25,
      totalCount: 1,
    });
    getStatistics.mockResolvedValue(STATS);

    renderPage();

    await waitFor(() => expect(screen.getByText("device-1")).toBeTruthy());
    // Indicadores compactos (también filtros rápidos): total y por estado.
    expect(screen.getByRole("button", { name: /Todas\s*10/ })).toBeTruthy();
    expect(screen.getByRole("button", { name: /Expiradas\s*4/ })).toBeTruthy();
    expect(screen.getAllByText("Activa").length).toBeGreaterThan(0);
  });

  it("muestra nombres de usuario y sucursal en lugar de GUID, sin columna ni filtro de empresa", async () => {
    setAuth("User");
    setPermissions(["access.sessions.view"]);
    getPaged.mockResolvedValue({ items: [ROW], pageNumber: 1, pageSize: 25, totalCount: 1 });
    getStatistics.mockResolvedValue(STATS);

    renderPage();

    await waitFor(() => expect(screen.getByText("Juan Pérez")).toBeTruthy());
    expect(screen.getByText("@jperez")).toBeTruthy();
    expect(screen.getByText("Matriz")).toBeTruthy();
    expect(screen.queryByText("Empresa")).toBeNull();
    expect(screen.queryByText(/user-111/)).toBeNull();
    expect(screen.queryByText(/Todas las empresas/)).toBeNull();
    expect(getPaged).toHaveBeenCalledWith(
      expect.not.objectContaining({ companyId: expect.anything() }),
    );
  });

  it("si el backend no resuelve nombres, muestra el identificador abreviado", async () => {
    setAuth("User");
    setPermissions(["access.sessions.view"]);
    getPaged.mockResolvedValue({
      items: [{ ...ROW, userFullName: null, username: null, branchName: null }],
      pageNumber: 1,
      pageSize: 25,
      totalCount: 1,
    });
    getStatistics.mockResolvedValue(STATS);

    renderPage();

    await waitFor(() => expect(screen.getByText("user-111…")).toBeTruthy());
    expect(screen.getByText("branch-1…")).toBeTruthy();
  });

  it("empresa admin (rol Admin) también accede aunque no tenga permisos explícitos", async () => {
    setAuth("Admin");
    getPaged.mockResolvedValue({
      items: [],
      pageNumber: 1,
      pageSize: 25,
      totalCount: 0,
    });
    getStatistics.mockResolvedValue(STATS);

    renderPage();

    await waitFor(() => expect(getPaged).toHaveBeenCalled());
    expect(
      screen.getByText("No se encontraron sesiones con los filtros indicados."),
    ).toBeTruthy();
  });

  it('sin permiso de cierre, oculta el botón "Cerrar sesión"', async () => {
    setAuth("User");
    setPermissions(["access.sessions.view"]);
    getPaged.mockResolvedValue({
      items: [ROW],
      pageNumber: 1,
      pageSize: 25,
      totalCount: 1,
    });
    getStatistics.mockResolvedValue(STATS);

    renderPage();

    await waitFor(() => expect(screen.getByText("device-1")).toBeTruthy());
    expect(screen.queryByText("Cerrar sesión")).toBeNull();
  });

  it("con permiso de cierre, confirma en el modal y llama al servicio de cierre", async () => {
    setAuth("User");
    setPermissions(["access.sessions.view", "access.sessions.close"]);
    getPaged.mockResolvedValue({
      items: [ROW],
      pageNumber: 1,
      pageSize: 25,
      totalCount: 1,
    });
    getStatistics.mockResolvedValue(STATS);
    close.mockResolvedValue("Sesión cerrada correctamente.");

    renderPage();

    await waitFor(() => expect(screen.getByText("device-1")).toBeTruthy());

    fireEvent.click(screen.getByText("Cerrar sesión"));
    expect(screen.getByRole("alertdialog")).toBeTruthy();

    const confirmButtons = screen.getAllByText("Cerrar sesión");
    fireEvent.click(confirmButtons[confirmButtons.length - 1]);

    await waitFor(() => expect(close).toHaveBeenCalledWith("session-1"));
    await waitFor(() => expect(getPaged).toHaveBeenCalledTimes(2)); // carga inicial + recarga tras cerrar
  });

  it("el filtro de estado dispara una nueva consulta con el status seleccionado", async () => {
    setAuth("User");
    setPermissions(["access.sessions.view"]);
    getPaged.mockResolvedValue({
      items: [],
      pageNumber: 1,
      pageSize: 25,
      totalCount: 0,
    });
    getStatistics.mockResolvedValue(STATS);

    renderPage();
    await waitFor(() => expect(getPaged).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByDisplayValue("Todos los estados"), {
      target: { value: "Expired" },
    });

    await waitFor(() => expect(getPaged).toHaveBeenCalledTimes(2));
    expect(getPaged).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: "Expired" }),
    );
  });

  it("el indicador de estado actúa como filtro rápido y 'Limpiar filtros' lo restablece", async () => {
    setAuth("User");
    setPermissions(["access.sessions.view"]);
    getPaged.mockResolvedValue({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 });
    getStatistics.mockResolvedValue(STATS);

    renderPage();
    await waitFor(() => expect(getPaged).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole("button", { name: /Expiradas\s*4/ }));
    await waitFor(() =>
      expect(getPaged).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: "Expired", pageNumber: 1 }),
      ),
    );

    fireEvent.click(screen.getByText("Limpiar filtros"));
    await waitFor(() =>
      expect(getPaged).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: undefined }),
      ),
    );
  });

  it("cambiar un filtro desde otra página vuelve a la página 1", async () => {
    setAuth("User");
    setPermissions(["access.sessions.view"]);
    getPaged.mockResolvedValue({ items: [ROW], pageNumber: 1, pageSize: 25, totalCount: 60 });
    getStatistics.mockResolvedValue(STATS);

    const { container } = renderPage();
    await waitFor(() => expect(screen.getByText("device-1")).toBeTruthy());

    const [, next] = container.querySelectorAll<HTMLButtonElement>(".pg-pagination-btn");
    fireEvent.click(next);
    await waitFor(() =>
      expect(getPaged).toHaveBeenLastCalledWith(expect.objectContaining({ pageNumber: 2 })),
    );

    fireEvent.change(screen.getByDisplayValue("Todos los estados"), {
      target: { value: "Active" },
    });
    await waitFor(() =>
      expect(getPaged).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: "Active", pageNumber: 1 }),
      ),
    );
  });

  it("ofrece el filtro por usuario solo con permiso de membresías y filtra por identityUserId", async () => {
    setAuth("User");
    setPermissions(["access.sessions.view", "access.company_user_memberships.view"]);
    getPaged.mockResolvedValue({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 });
    getStatistics.mockResolvedValue(STATS);
    listMembers.mockResolvedValue([
      { identityUserId: "u-1", fullName: "Juan Pérez", username: "jperez", role: "User", isActive: true },
    ]);

    renderPage();
    await waitFor(() => expect(screen.getByText("Juan Pérez (@jperez)")).toBeTruthy());

    fireEvent.change(screen.getByDisplayValue("Todos los usuarios"), {
      target: { value: "u-1" },
    });
    await waitFor(() =>
      expect(getPaged).toHaveBeenLastCalledWith(
        expect.objectContaining({ identityUserId: "u-1" }),
      ),
    );
  });

  it("sin permiso de membresías no muestra el filtro por usuario ni consulta membresías", async () => {
    setAuth("User");
    setPermissions(["access.sessions.view"]);
    getPaged.mockResolvedValue({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 });
    getStatistics.mockResolvedValue(STATS);

    renderPage();
    await waitFor(() => expect(getPaged).toHaveBeenCalled());

    expect(screen.queryByDisplayValue("Todos los usuarios")).toBeNull();
    expect(listMembers).not.toHaveBeenCalled();
  });
});
