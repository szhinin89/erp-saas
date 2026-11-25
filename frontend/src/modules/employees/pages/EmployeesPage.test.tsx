// @vitest-environment jsdom
/** EMP-E7 (T-14) — listado: contrato de consulta, filtros, paginación, permisos y acceso a la ficha. */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { apiGet } from "../../lib/apiEnvelope";
import { EmployeesPage } from "./EmployeesPage";

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiPatch: vi.fn(),
  apiDelete: vi.fn(),
}));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));

const ALL = [
  "employees.view",
  "employees.create",
  "employees.catalogs.view",
  "settings.branches.view",
];

function grant(...keys: string[]) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (k: string) => keys.includes(k),
    has: (k: string) => keys.includes(k),
    isAdminRole: false,
  });
}

const ROW = {
  id: "emp-1",
  companyId: "c1",
  businessPartner: { id: "bp-1", displayName: "Ana Pérez", identificationType: "05", identificationNumber: "0102030405" },
  isActive: true,
  jobPosition: { id: "jp-old", name: "Cajero", isActive: false },
  profession: null,
  rowVersion: 7,
};

const urls = () => vi.mocked(apiGet).mock.calls.map(([url]) => url);
const listParams = () =>
  vi
    .mocked(apiGet)
    .mock.calls.filter(([url]) => url === "/api/v1/employees")
    .map(([, config]) => config?.params);

function renderPage() {
  const router = createMemoryRouter(
    [
      { path: "/employees", element: <EmployeesPage /> },
      { path: "/employees/:id", element: <p>FICHA</p> },
    ],
    { initialEntries: ["/employees"] },
  );
  render(
    <I18nProvider>
      <RouterProvider router={router} />
    </I18nProvider>,
  );
  return router;
}

beforeEach(() => {
  grant(...ALL);
  vi.mocked(apiGet).mockImplementation(async (url: string) => {
    if (url === "/api/v1/employees")
      return { items: [ROW], pageNumber: 1, pageSize: 25, totalCount: 60 } as never;
    if (url.startsWith("/api/v1/employees/job-positions"))
      return { items: [{ id: "jp-1", name: "Estilista", isActive: true, rowVersion: 1 }], totalCount: 1 } as never;
    if (url.startsWith("/api/v1/employees/")) return { items: [], totalCount: 0 } as never;
    if (url.startsWith("/api/v1/settings/branches"))
      return [{ id: "br-1", name: "Matriz", isActive: true }] as never;
    throw new Error(`URL inesperada ${url}`);
  });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("EmployeesPage", () => {
  it("lista con paginación del backend y marca el catálogo inactivo asignado", async () => {
    renderPage();
    expect(await screen.findByText("Ana Pérez")).toBeTruthy();
    expect(screen.getByText("Cajero (inactivo)")).toBeTruthy();
    expect(listParams()[0]).toMatchObject({ skip: 0, take: 25 });
    expect(listParams()[0]?.isActive).toBeUndefined();
    expect(listParams()[0]?.asOf).toBeUndefined();
  });

  it("envía búsqueda, estado, cargo y sucursal con fecha; la fecha solo viaja con sucursal", async () => {
    renderPage();
    await screen.findByText("Ana Pérez");
    expect((screen.getByLabelText("Asignado en la fecha") as HTMLInputElement).disabled).toBe(true);
    await screen.findByRole("option", { name: "Estilista" });
    await screen.findByRole("option", { name: "Matriz" });

    fireEvent.change(screen.getByLabelText("Buscar"), { target: { value: " ana " } });
    fireEvent.change(screen.getByLabelText("Estado"), { target: { value: "inactive" } });
    fireEvent.change(screen.getByLabelText("Cargo"), { target: { value: "jp-1" } });
    fireEvent.change(screen.getByLabelText("Sucursal"), { target: { value: "br-1" } });
    fireEvent.change(screen.getByLabelText("Asignado en la fecha"), { target: { value: "2026-03-01" } });

    await waitFor(() =>
      expect(listParams().at(-1)).toMatchObject({
        search: "ana",
        isActive: false,
        jobPositionId: "jp-1",
        branchId: "br-1",
        asOf: "2026-03-01",
        skip: 0,
      }),
    );
  });

  it("pagina con skip y un filtro nuevo vuelve a la primera página", async () => {
    renderPage();
    await screen.findByText("Ana Pérez");
    fireEvent.click(screen.getByRole("button", { name: /siguiente|next|chevron_right/i }));
    await waitFor(() => expect(listParams().at(-1)).toMatchObject({ skip: 25 }));
    await screen.findByText("Ana Pérez");
    fireEvent.change(screen.getByLabelText("Estado"), { target: { value: "active" } });
    await waitFor(() => expect(listParams().at(-1)).toMatchObject({ skip: 0, isActive: true }));
  });

  it("abre la ficha desde la lista", async () => {
    const router = renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Ver ficha Ana Pérez" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/employees/emp-1"));
    // Una sola entrada de historial: «atrás» vuelve al listado.
    await router.navigate(-1);
    expect(router.state.location.pathname).toBe("/employees");
  });

  it("sin employees.view no consulta nada", () => {
    grant("employees.create");
    renderPage();
    expect(screen.queryByText("Ana Pérez")).toBeNull();
    expect(apiGet).not.toHaveBeenCalled();
  });

  it("sin permisos de selectores no los consulta, lo explica y conserva el listado", async () => {
    grant("employees.view");
    renderPage();
    expect(await screen.findByText("Ana Pérez")).toBeTruthy();
    expect(screen.getByText(/requieren el permiso de consulta de catálogos/)).toBeTruthy();
    expect(screen.getByText(/requiere el permiso de consulta de sucursales/)).toBeTruthy();
    expect(screen.queryByLabelText("Cargo")).toBeNull();
    expect(screen.queryByRole("button", { name: /Nuevo empleado/ })).toBeNull();
    expect(urls().every((url) => url === "/api/v1/employees")).toBe(true);
  });
});
