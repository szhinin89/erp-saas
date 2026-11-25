// @vitest-environment jsdom
/** EMP-E7 (T-14) — catálogos: Lista → Editor por pestaña, versiones, conflictos, permisos y guard. */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { AxiosError, type AxiosResponse } from "axios";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAuthStore } from "../../../store/authStore";
import { message } from "../../../lib/messages";
import { apiDelete, apiGet, apiPatch, apiPost } from "../../lib/apiEnvelope";
import { EmployeeCatalogsPage } from "./EmployeeCatalogsPage";

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiPatch: vi.fn(),
  apiDelete: vi.fn(),
}));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

function grant(...keys: string[]) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (k: string) => keys.includes(k),
    has: (k: string) => keys.includes(k),
    isAdminRole: false,
  });
}

const BASE = "/api/v1/employees/job-positions";
const ACTIVE = { id: "jp-1", name: "Estilista", isActive: true, rowVersion: 3 };
const INACTIVE = { id: "jp-2", name: "Cajero", isActive: false, rowVersion: 4 };

const apiError = (status: number, code: string, user: string) =>
  new AxiosError("x", undefined, undefined, undefined, {
    status,
    data: { code, message: { user } },
  } as AxiosResponse);

const lastList = () =>
  vi.mocked(apiGet).mock.calls.filter(([, c]) => c?.params).at(-1);

function renderPage() {
  const router = createMemoryRouter([{ path: "/", element: <EmployeeCatalogsPage /> }]);
  render(
    <I18nProvider>
      <RouterProvider router={router} />
    </I18nProvider>,
  );
}

const nameBox = () => screen.getByLabelText("Nombre", { selector: "form input" }) as HTMLInputElement;
const button = (name: string | RegExp) => screen.getByRole("button", { name });

beforeEach(() => {
  useAuthStore.setState({ isAuthenticated: true });
  grant("employees.catalogs.view", "employees.catalogs.manage");
  vi.mocked(apiGet).mockImplementation(async (url: string) => {
    if (url === `${BASE}/jp-1`) return { ...ACTIVE, name: "Estilista senior", rowVersion: 9 } as never;
    return { items: [ACTIVE, INACTIVE], totalCount: 2 } as never;
  });
  vi.mocked(apiPost).mockResolvedValue(ACTIVE);
  vi.mocked(apiPatch).mockResolvedValue(ACTIVE);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("catálogos laborales", () => {
  it("tiene una pestaña por catálogo y consulta el elegido con sus filtros", async () => {
    renderPage();
    await screen.findByText("Estilista");
    expect(lastList()?.[0]).toBe(BASE);
    for (const [tab, path] of [
      ["Profesiones", "professions"],
      ["Especialidades", "specialties"],
    ]) {
      fireEvent.click(screen.getByRole("tab", { name: new RegExp(tab) }));
      await waitFor(() => expect(lastList()?.[0]).toBe(`/api/v1/employees/${path}`));
    }
    fireEvent.change(screen.getByLabelText("Estado"), { target: { value: "inactive" } });
    await waitFor(() =>
      expect(lastList()?.[1]?.params).toMatchObject({ isActive: false, skip: 0, take: 25 }),
    );
  });

  it("crea y vuelve a la lista", async () => {
    renderPage();
    await screen.findByText("Estilista");
    fireEvent.click(button(/Nuevo registro$/));
    fireEvent.click(button(/Crear/));
    expect(await screen.findByText("El nombre es obligatorio.")).toBeTruthy();
    fireEvent.change(nameBox(), { target: { value: "  Barbero " } });
    fireEvent.click(button(/Crear/));
    await waitFor(() => expect(apiPost).toHaveBeenCalledExactlyOnceWith(BASE, { name: "Barbero" }));
    await waitFor(() => expect(message.success).toHaveBeenCalled());
    expect(await screen.findByText("Estilista")).toBeTruthy();
  });

  it("edita con rowVersion; ante conflicto recarga el registro y no reintenta", async () => {
    vi.mocked(apiPatch).mockRejectedValueOnce(
      apiError(409, "CONCURRENCY_CONFLICT", "El registro fue modificado."),
    );
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Editar Estilista" }));
    fireEvent.change(nameBox(), { target: { value: "Mío" } });
    fireEvent.click(button(/Guardar/));
    await waitFor(() => expect(nameBox().value).toBe("Estilista senior"));
    expect(message.error).toHaveBeenCalledWith("El registro fue modificado.");
    expect(apiPatch).toHaveBeenCalledExactlyOnceWith(`${BASE}/jp-1`, { rowVersion: 3, name: "Mío" });

    fireEvent.change(nameBox(), { target: { value: "Mío" } });
    fireEvent.click(button(/Guardar/));
    await waitFor(() =>
      expect(apiPatch).toHaveBeenLastCalledWith(`${BASE}/jp-1`, { rowVersion: 9, name: "Mío" }),
    );
  });

  it("muestra el duplicado devuelto por el servidor", async () => {
    vi.mocked(apiPost).mockRejectedValue(
      apiError(409, "EMPLOYEE_CATALOG_DUPLICATE", "Ya existe un registro con ese nombre."),
    );
    renderPage();
    await screen.findByText("Estilista");
    fireEvent.click(button(/Nuevo registro$/));
    fireEvent.change(nameBox(), { target: { value: "Estilista" } });
    fireEvent.click(button(/Crear/));
    expect(await screen.findByText("Ya existe un registro con ese nombre.")).toBeTruthy();
  });

  it("activa y desactiva con confirmación y versión; nunca elimina", async () => {
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Desactivar Estilista" }));
    fireEvent.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Desactivar" }));
    await waitFor(() =>
      expect(apiPost).toHaveBeenLastCalledWith(`${BASE}/jp-1/deactivate`, { rowVersion: 3 }),
    );
    fireEvent.click(await screen.findByRole("button", { name: "Activar Cajero" }));
    fireEvent.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Activar" }));
    await waitFor(() =>
      expect(apiPost).toHaveBeenLastCalledWith(`${BASE}/jp-2/activate`, { rowVersion: 4 }),
    );
    expect(screen.queryByRole("button", { name: /Eliminar/ })).toBeNull();
    expect(apiDelete).not.toHaveBeenCalled();
  });

  it("cambiar de catálogo con cambios pide confirmación", async () => {
    vi.mocked(message.confirm).mockResolvedValueOnce(false).mockResolvedValueOnce(true);
    renderPage();
    await screen.findByText("Estilista");
    fireEvent.click(button(/Nuevo registro$/));
    fireEvent.change(nameBox(), { target: { value: "Sin guardar" } });
    fireEvent.click(screen.getByRole("tab", { name: /Profesiones/ }));
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    expect(nameBox().value).toBe("Sin guardar");
    fireEvent.click(screen.getByRole("tab", { name: /Profesiones/ }));
    await waitFor(() => expect(lastList()?.[0]).toBe("/api/v1/employees/professions"));
  });

  it("solo consulta: sin acciones; sin consulta: no pide nada", async () => {
    grant("employees.catalogs.view");
    renderPage();
    await screen.findByText("Estilista");
    expect(screen.queryByRole("button", { name: /Nuevo registro|Editar|Desactivar/ })).toBeNull();
    cleanup();
    vi.clearAllMocks();
    grant("employees.view");
    renderPage();
    expect(apiGet).not.toHaveBeenCalled();
  });
});
