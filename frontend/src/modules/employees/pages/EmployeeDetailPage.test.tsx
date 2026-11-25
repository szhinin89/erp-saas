// @vitest-environment jsdom
/**
 * EMP-E7 (T-14) — ficha: tres secciones, edición, ciclo laboral, sucursales, usuario, versiones,
 * conflictos, avisos, permisos y cambios sin guardar. Solo se simula la capa HTTP.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { AxiosError, type AxiosResponse } from "axios";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAuthStore } from "../../../store/authStore";
import { message } from "../../../lib/messages";
import { apiGet, apiPatch, apiPost, apiPut } from "../../lib/apiEnvelope";
import type { EmployeeDto } from "../types/employee.types";
import { EmployeeDetailPage } from "./EmployeeDetailPage";

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

const ALL = [
  "employees.view",
  "employees.update",
  "employees.disable",
  "employees.assignments.manage",
  "employees.link-user",
  "employees.catalogs.view",
  "settings.branches.view",
  "access.company_user_memberships.view",
];

function grant(...keys: string[]) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (k: string) => keys.includes(k),
    has: (k: string) => keys.includes(k),
    isAdminRole: false,
  });
}
const without = (...keys: string[]) => grant(...ALL.filter((k) => !keys.includes(k)));

const BASE = "/api/v1/employees/emp-1";
const MEMBERSHIPS = "/api/v1/admin/iam/memberships";

const employee = (over: Partial<EmployeeDto> = {}): EmployeeDto => ({
  id: "emp-1",
  companyId: "c1",
  businessPartner: { id: "bp-1", displayName: "Ana Pérez", identificationType: "05", identificationNumber: "0102030405" },
  isActive: true,
  jobPosition: { id: "jp-old", name: "Cajero", isActive: false },
  profession: { id: "pr-1", name: "Cosmetóloga", isActive: true },
  specialties: [{ id: "sp-old", name: "Color", isActive: false }],
  identityUser: null,
  periods: [{ id: "per-1", startDate: "2026-01-10", endDate: null, terminationReason: null }],
  assignments: [
    { id: "as-1", branchId: "br-1", branchName: "Matriz", branchCode: "001", startDate: "2026-01-10", endDate: null, rowVersion: 31 },
    { id: "as-2", branchId: "br-2", branchName: "Norte", branchCode: null, startDate: "2099-01-01", endDate: "2099-02-01", rowVersion: 32 },
    { id: "as-3", branchId: "br-3", branchName: "Sur", branchCode: null, startDate: "2020-01-01", endDate: "2020-02-01", rowVersion: 33 },
  ],
  rowVersion: 10,
  warnings: [],
  ...over,
});

let current: EmployeeDto;

const apiError = (status: number, code: string, user: string) =>
  new AxiosError("x", undefined, undefined, undefined, {
    status,
    data: { code, message: { user } },
  } as AxiosResponse);

const urls = () => vi.mocked(apiGet).mock.calls.map(([url]) => url);
const reads = () => urls().filter((u) => u === BASE).length;

function renderPage(tab?: string) {
  const router = createMemoryRouter(
    [
      { path: "/employees/:id", element: <EmployeeDetailPage /> },
      { path: "/employees", element: <p>LISTADO</p> },
    ],
    { initialEntries: [`/employees/emp-1${tab ? `?tab=${tab}` : ""}`] },
  );
  render(
    <I18nProvider>
      <RouterProvider router={router} />
    </I18nProvider>,
  );
  return router;
}

const ready = () => screen.findByRole("tab", { name: "General" });
const button = (name: string | RegExp) => screen.getByRole("button", { name });
const queryButton = (name: string | RegExp) => screen.queryByRole("button", { name });
const dialog = () => within(screen.getByRole("dialog"));
const change = (label: RegExp | string, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });

beforeEach(() => {
  current = employee();
  useAuthStore.setState({ isAuthenticated: true });
  grant(...ALL);
  vi.mocked(apiGet).mockImplementation(async (url: string) => {
    if (url === BASE) return current as never;
    if (url.startsWith("/api/v1/employees/job-positions"))
      return { items: [{ id: "jp-1", name: "Estilista", isActive: true, rowVersion: 1 }], totalCount: 1 } as never;
    if (url.startsWith("/api/v1/employees/professions"))
      return { items: [{ id: "pr-1", name: "Cosmetóloga", isActive: true, rowVersion: 1 }], totalCount: 1 } as never;
    if (url.startsWith("/api/v1/employees/specialties"))
      return {
        items: [
          { id: "sp-1", name: "Corte", isActive: true, rowVersion: 1 },
          { id: "sp-old", name: "Color", isActive: false, rowVersion: 1 },
        ],
        totalCount: 2,
      } as never;
    if (url.startsWith("/api/v1/settings/branches"))
      return [{ id: "br-9", name: "Centro", isActive: true }] as never;
    if (url === MEMBERSHIPS)
      return [{ identityUserId: "u-1", username: "ana", fullName: "Ana Pérez U", isActive: true }] as never;
    return [] as never;
  });
  vi.mocked(apiPost).mockResolvedValue(employee({ rowVersion: 11 }));
  vi.mocked(apiPatch).mockResolvedValue(employee({ rowVersion: 11 }));
  vi.mocked(apiPut).mockResolvedValue(employee({ rowVersion: 12 }));
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("ficha: General", () => {
  it("muestra tres secciones, identidad en solo lectura e historial", async () => {
    renderPage();
    await ready();
    expect(screen.getAllByRole("tab").map((x) => x.textContent)).toEqual([
      "General",
      "Sucursales",
      "Acceso ERP",
    ]);
    expect(screen.getAllByText("0102030405").length).toBeGreaterThan(0);
    expect(screen.getByText("10/01/2026")).toBeTruthy();
    expect(screen.queryByDisplayValue("Ana Pérez")).toBeNull();
    await screen.findByRole("option", { name: "Estilista" });
    expect(screen.getByRole("option", { name: "Cajero (inactivo)" })).toBeTruthy();
    expect(screen.getByRole("button", { name: /Quitar Color \(inactivo\)/ })).toBeTruthy();
  });

  it("PATCH envía solo lo cambiado y conserva el cargo inactivo omitiéndolo; null quita", async () => {
    renderPage();
    await ready();
    await screen.findByRole("option", { name: "Estilista" });
    change("Profesión", "");
    fireEvent.click(button(/Guardar/));
    await waitFor(() =>
      expect(apiPatch).toHaveBeenCalledExactlyOnceWith(BASE, { rowVersion: 10, professionId: null }),
    );
    expect(apiPut).not.toHaveBeenCalled();
    await waitFor(() => expect(reads()).toBe(2));
  });

  it("guarda especialidades con la versión devuelta por el PATCH", async () => {
    renderPage();
    await ready();
    await screen.findByRole("option", { name: "Estilista" });
    change("Cargo", "jp-1");
    const box = screen.getByRole("combobox", { name: "Especialidades" });
    fireEvent.focus(box);
    fireEvent.click(await screen.findByRole("option", { name: "Corte" }));
    fireEvent.click(button(/Guardar/));
    await waitFor(() =>
      expect(apiPut).toHaveBeenCalledExactlyOnceWith(`${BASE}/specialties`, {
        rowVersion: 11,
        specialtyIds: ["sp-1", "sp-old"],
      }),
    );
    expect(apiPatch).toHaveBeenCalledWith(BASE, { rowVersion: 10, jobPositionId: "jp-1" });
  });

  it("ante conflicto muestra el mensaje, recarga, descarta lo rechazado y no reintenta", async () => {
    vi.mocked(apiPatch).mockRejectedValueOnce(
      apiError(409, "CONCURRENCY_CONFLICT", "El registro fue modificado."),
    );
    renderPage();
    await ready();
    await screen.findByRole("option", { name: "Estilista" });
    change("Cargo", "jp-1");
    current = employee({ rowVersion: 20 });
    fireEvent.click(button(/Guardar/));

    await waitFor(() => expect(message.error).toHaveBeenCalledWith("El registro fue modificado."));
    await waitFor(() =>
      expect((screen.getByLabelText("Cargo") as HTMLSelectElement).value).toBe("jp-old"),
    );
    expect(apiPatch).toHaveBeenCalledOnce();
    expect(reads()).toBe(2);

    change("Cargo", "jp-1");
    fireEvent.click(button(/Guardar/));
    await waitFor(() =>
      expect(apiPatch).toHaveBeenLastCalledWith(BASE, { rowVersion: 20, jobPositionId: "jp-1" }),
    );
  });

  it("sin consulta de catálogos queda en solo lectura, lo explica y no los pide", async () => {
    without("employees.catalogs.view");
    renderPage();
    await ready();
    expect(screen.getByText(/necesita el permiso de consulta de catálogos/)).toBeTruthy();
    expect(screen.getByText("Cajero (inactivo)")).toBeTruthy();
    expect(screen.queryByLabelText("Cargo")).toBeNull();
    expect(urls().some((u) => u.startsWith("/api/v1/employees/job-positions"))).toBe(false);
    expect(button(/Dar de baja/)).toBeTruthy();
  });

  it("con cambios sin guardar pide confirmación al salir; cambiar de pestaña no", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const router = renderPage();
    await ready();
    await screen.findByRole("option", { name: "Estilista" });
    change("Cargo", "jp-1");
    fireEvent.click(screen.getByRole("tab", { name: "Sucursales" }));
    expect(message.confirm).not.toHaveBeenCalled();
    fireEvent.click(button(/Volver a empleados/));
    await waitFor(() => expect(message.confirm).toHaveBeenCalledOnce());
    expect(router.state.location.pathname).toBe("/employees/emp-1");
  });

  it("empleado inexistente u otra empresa: no encontrado; sin permiso: no consulta", async () => {
    vi.mocked(apiGet).mockImplementation(async (url: string) => {
      if (url === BASE) throw apiError(404, "EMPLOYEE_NOT_FOUND", "El empleado no existe.");
      return { items: [], totalCount: 0 } as never;
    });
    renderPage();
    expect(await screen.findByText("El empleado no existe en la empresa activa.")).toBeTruthy();
    cleanup();
    vi.clearAllMocks();
    grant();
    renderPage();
    expect(apiGet).not.toHaveBeenCalled();
  });
});

describe("ficha: baja y reincorporación", () => {
  it("da de baja con la versión de la ficha y avisa la membresía activa sin bloquear", async () => {
    vi.mocked(apiPost).mockResolvedValue(employee({ warnings: ["ACTIVE_MEMBERSHIP"], isActive: false }));
    renderPage();
    await ready();
    fireEvent.click(button(/Dar de baja/));
    change(/Fecha de fin/, "2026-03-31");
    change("Motivo", " Renuncia ");
    current = employee({
      isActive: false,
      rowVersion: 11,
      periods: [{ id: "per-1", startDate: "2026-01-10", endDate: "2026-03-31", terminationReason: "Renuncia" }],
    });
    fireEvent.click(dialog().getByRole("button", { name: "Dar de baja" }));

    await waitFor(() =>
      expect(apiPost).toHaveBeenCalledExactlyOnceWith(`${BASE}/terminate`, {
        rowVersion: 10,
        endDate: "2026-03-31",
        reason: "Renuncia",
      }),
    );
    expect(await screen.findByText(/conserva su membresía activa/)).toBeTruthy();
    expect(screen.getByText(/no revoca automáticamente el acceso/)).toBeTruthy();
    expect(message.success).toHaveBeenCalledOnce();
    expect(message.error).not.toHaveBeenCalled();
    expect(button(/Reincorporar/)).toBeTruthy();
    // El aviso no se persiste: una carga nueva de la ficha ya no lo muestra.
    cleanup();
    renderPage();
    await ready();
    expect(screen.queryByText(/conserva su membresía activa/)).toBeNull();
  });

  it("muestra el bloqueo por asignaciones futuras sin cerrar el diálogo", async () => {
    vi.mocked(apiPost).mockRejectedValue(
      apiError(422, "EMPLOYEE_TERMINATION_BLOCKED_BY_FUTURE_ASSIGNMENTS", "Existen asignaciones posteriores."),
    );
    renderPage();
    await ready();
    fireEvent.click(button(/Dar de baja/));
    fireEvent.click(dialog().getByRole("button", { name: "Dar de baja" }));
    await waitFor(() => expect(message.error).toHaveBeenCalledWith("Existen asignaciones posteriores."));
    expect(screen.getByRole("dialog")).toBeTruthy();
  });

  it("valida el fin anterior al inicio y la reincorporación el mismo día", async () => {
    renderPage();
    await ready();
    fireEvent.click(button(/Dar de baja/));
    change(/Fecha de fin/, "2026-01-01");
    fireEvent.click(dialog().getByRole("button", { name: "Dar de baja" }));
    expect(await screen.findByText("La fecha de fin no puede ser anterior al inicio.")).toBeTruthy();
    expect(apiPost).not.toHaveBeenCalled();
    cleanup();

    current = employee({
      isActive: false,
      periods: [{ id: "per-1", startDate: "2026-01-10", endDate: "2026-03-31", terminationReason: null }],
    });
    renderPage();
    await ready();
    fireEvent.click(button(/Reincorporar/));
    change(/Fecha de ingreso/, "2026-03-31");
    fireEvent.click(dialog().getByRole("button", { name: "Reincorporar" }));
    expect(await screen.findByText(/posterior a la última baja/)).toBeTruthy();
    expect(apiPost).not.toHaveBeenCalled();
    change(/Fecha de ingreso/, "2026-04-01");
    fireEvent.click(dialog().getByRole("button", { name: "Reincorporar" }));
    await waitFor(() =>
      expect(apiPost).toHaveBeenCalledExactlyOnceWith(`${BASE}/rehire`, {
        rowVersion: 10,
        startDate: "2026-04-01",
      }),
    );
  });

  it("sin employees.disable no ofrece baja", async () => {
    without("employees.disable");
    renderPage();
    await ready();
    expect(queryButton(/Dar de baja/)).toBeNull();
  });
});

describe("ficha: Sucursales", () => {
  it("clasifica vigentes, futuras e históricas y crea una asignación", async () => {
    renderPage("branches");
    await ready();
    expect(screen.getByText("Vigente", { selector: ".badge" })).toBeTruthy();
    expect(screen.getByText("Futura")).toBeTruthy();
    expect(screen.getByText("Histórica")).toBeTruthy();
    fireEvent.click(button(/Nueva asignación/));
    await screen.findByRole("option", { name: "Centro" });
    fireEvent.change(dialog().getByLabelText(/Sucursal/), { target: { value: "br-9" } });
    change(/Desde/, "2026-05-01");
    fireEvent.click(dialog().getByRole("button", { name: "Asignar" }));
    await waitFor(() =>
      expect(apiPost).toHaveBeenCalledExactlyOnceWith(`${BASE}/branch-assignments`, {
        branchId: "br-9",
        startDate: "2026-05-01",
        endDate: undefined,
      }),
    );
    await waitFor(() => expect(reads()).toBe(2));
  });

  it("cierra con la versión de la asignación, no la de la ficha", async () => {
    renderPage("branches");
    await ready();
    expect(screen.getAllByRole("button", { name: /Cerrar asignación/ })).toHaveLength(1);
    fireEvent.click(button("Cerrar asignación Matriz"));
    change(/Hasta/, "2026-06-30");
    fireEvent.click(dialog().getByRole("button", { name: "Cerrar asignación" }));
    await waitFor(() =>
      expect(apiPost).toHaveBeenCalledExactlyOnceWith(`${BASE}/branch-assignments/as-1/close`, {
        rowVersion: 31,
        endDate: "2026-06-30",
      }),
    );
  });

  it("sin consulta de sucursales no ofrece crear ni consulta, pero permite cerrar", async () => {
    without("settings.branches.view");
    renderPage("branches");
    await ready();
    expect(queryButton(/Nueva asignación/)).toBeNull();
    expect(screen.getByText(/necesita el permiso de consulta de sucursales/)).toBeTruthy();
    expect(button("Cerrar asignación Matriz")).toBeTruthy();
    expect(urls().some((u) => u.includes("settings/branches"))).toBe(false);
  });

  it("un empleado de baja no admite asignaciones nuevas; sin permiso no hay acciones", async () => {
    current = employee({ isActive: false });
    renderPage("branches");
    await ready();
    expect((button(/Nueva asignación/) as HTMLButtonElement).disabled).toBe(true);
    cleanup();
    without("employees.assignments.manage");
    renderPage("branches");
    await ready();
    expect(queryButton(/asignación/)).toBeNull();
  });
});

describe("ficha: Acceso ERP", () => {
  it("sin usuario visible no afirma que no exista vínculo y vincula con la versión de la ficha", async () => {
    renderPage("access");
    await ready();
    expect(screen.getByText(/Puede existir un vínculo con una cuenta protegida/)).toBeTruthy();
    fireEvent.click(button(/^link Vincular usuario|^Vincular usuario/));
    const box = await screen.findByRole("combobox", { name: "Usuario ERP" });
    await waitFor(() => expect((box as HTMLInputElement).disabled).toBe(false));
    fireEvent.focus(box);
    fireEvent.click(await screen.findByRole("option", { name: /Ana Pérez U/ }));
    fireEvent.click(dialog().getByRole("button", { name: "Vincular usuario" }));
    await waitFor(() =>
      expect(apiPost).toHaveBeenCalledExactlyOnceWith(`${BASE}/link-user`, {
        identityUserId: "u-1",
        rowVersion: 10,
      }),
    );
    expect(vi.mocked(apiGet).mock.calls.find(([u]) => u === MEMBERSHIPS)?.[1]).toEqual({
      params: { onlyActive: true },
    });
  });

  it("desvincula solo con employees.link-user, sin consultar membresías", async () => {
    without("access.company_user_memberships.view");
    renderPage("access");
    await ready();
    expect(queryButton(/^Vincular usuario/)).toBeNull();
    expect(screen.getByText(/necesita además el permiso de consulta de usuarios/)).toBeTruthy();
    fireEvent.click(button(/Desvincular usuario/));
    fireEvent.click(
      within(screen.getByRole("alertdialog")).getByRole("button", { name: "Desvincular usuario" }),
    );
    await waitFor(() =>
      expect(apiPost).toHaveBeenCalledExactlyOnceWith(`${BASE}/unlink-user`, { rowVersion: 10 }),
    );
    expect(urls()).not.toContain(MEMBERSHIPS);
  });

  it("muestra el estado real de la membresía y el código de desvinculación sin vínculo", async () => {
    current = employee({ identityUser: { id: "u-1", username: "ana", membershipActive: false } });
    vi.mocked(apiPost).mockRejectedValue(
      apiError(422, "EMPLOYEE_USER_NOT_LINKED", "La ficha no tiene un usuario vinculado."),
    );
    renderPage("access");
    await ready();
    expect(screen.getByText("ana")).toBeTruthy();
    expect(screen.getByText("Inactiva")).toBeTruthy();
    expect(queryButton(/^Vincular usuario/)).toBeNull();
    fireEvent.click(button(/Desvincular usuario/));
    fireEvent.click(
      within(screen.getByRole("alertdialog")).getByRole("button", { name: "Desvincular usuario" }),
    );
    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith("La ficha no tiene un usuario vinculado."),
    );
  });

  it("sin employees.link-user no hay acciones de vínculo", async () => {
    without("employees.link-user");
    renderPage("access");
    await ready();
    expect(queryButton(/vincular usuario/i)).toBeNull();
  });
});
