// @vitest-environment jsdom
/**
 * EMP-E7 (T-14, D-1, P-06) — alta sobre el listado y wizard oficial de MasterData con rol
 * Employee. Se simula solo la capa HTTP: servicios, fachada y wizard son los reales.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { AxiosError, type AxiosResponse } from "axios";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAuthStore } from "../../../store/authStore";
import { message } from "../../../lib/messages";
import { apiGet, apiPost } from "../../lib/apiEnvelope";
import { EmployeeCreateModal } from "./EmployeeCreateModal";

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

const VIEW = "masterdata.businesspartners.view";
const CREATE = "masterdata.businesspartners.create";
const UPDATE = "masterdata.businesspartners.update";
const PARTNERS = "/api/v1/master/business-partners";

function grant(...keys: string[]) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (k: string) => keys.includes(k),
    has: (k: string) => keys.includes(k),
    isAdminRole: false,
  });
}

const BP = {
  id: "bp-1",
  identificationType: "05",
  identificationNumber: "0102030400",
  legalName: "Ana Pérez",
  isActive: true,
  isCustomer: true,
  isSupplier: false,
};

const apiError = (status: number, code: string, user: string) =>
  new AxiosError("x", undefined, undefined, undefined, {
    status,
    data: { code, message: { user } },
  } as AxiosResponse);

const urls = () => vi.mocked(apiGet).mock.calls.map(([url]) => url);
const onCreated = vi.fn();
const onClose = vi.fn();

function renderModal(canViewCatalogs = true) {
  const router = createMemoryRouter([
    {
      path: "/",
      element: (
        <EmployeeCreateModal canViewCatalogs={canViewCatalogs} onClose={onClose} onCreated={onCreated} />
      ),
    },
  ]);
  render(
    <I18nProvider>
      <RouterProvider router={router} />
    </I18nProvider>,
  );
}

const partnerBox = () => screen.getByRole("combobox", { name: "Socio de negocio con rol Empleado" });

async function pickPartner() {
  fireEvent.focus(partnerBox());
  fireEvent.change(partnerBox(), { target: { value: "ana" } });
  fireEvent.click(await screen.findByRole("option", { name: /Ana Pérez/ }));
}

const submit = () => fireEvent.click(screen.getByRole("button", { name: "Crear ficha" }));
const openWizard = () =>
  fireEvent.click(screen.getByRole("button", { name: /Registrar o asignar rol Empleado/ }));
async function searchInWizard() {
  fireEvent.change(screen.getByPlaceholderText("Cédula, pasaporte o nombres…"), {
    target: { value: "0102030400" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Buscar" }));
}

let partnerRows: unknown[] = [BP];

beforeEach(() => {
  partnerRows = [BP];
  useAuthStore.setState({ isAuthenticated: true });
  grant(VIEW, CREATE, UPDATE, "employees.create");
  vi.mocked(apiGet).mockImplementation(async (url: string) => {
    if (url.startsWith(`${PARTNERS}?`)) return { items: partnerRows } as never;
    if (url === `${PARTNERS}/bp-1`) return BP as never;
    if (url.includes("sri-id-types/by-usage/employee"))
      return [
        { code: "05", name: "Cédula" },
        { code: "06", name: "Pasaporte" },
      ] as never;
    if (url.startsWith("/api/v1/employees/specialties"))
      return { items: [], totalCount: 0 } as never;
    if (url.startsWith("/api/v1/employees/"))
      return {
        items: [
          { id: "jp-1", name: "Estilista", isActive: true, rowVersion: 1 },
          { id: "jp-2", name: "Retirado", isActive: false, rowVersion: 1 },
        ],
        totalCount: 2,
      } as never;
    return [] as never;
  });
  vi.mocked(apiPost).mockResolvedValue({ id: "emp-9" });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("alta de empleado", () => {
  it("busca solo socios activos con rol Employee y crea la ficha sin datos personales", async () => {
    renderModal();
    await screen.findAllByRole("option", { name: "Estilista" });
    expect(screen.queryByRole("option", { name: "Retirado" })).toBeNull();
    await pickPartner();
    expect(urls().find((u) => u.startsWith(`${PARTNERS}?`))).toContain("isActive=true");
    expect(urls().find((u) => u.startsWith(`${PARTNERS}?`))).toContain("roles=3");

    fireEvent.change(screen.getByLabelText(/Fecha de ingreso/), { target: { value: "2026-02-01" } });
    fireEvent.change(screen.getByLabelText("Cargo"), { target: { value: "jp-1" } });
    submit();

    await waitFor(() => expect(onCreated).toHaveBeenCalledWith({ id: "emp-9" }));
    expect(apiPost).toHaveBeenCalledExactlyOnceWith("/api/v1/employees", {
      businessPartnerId: "bp-1",
      startDate: "2026-02-01",
      jobPositionId: "jp-1",
      professionId: undefined,
      specialtyIds: undefined,
    });
  });

  it("exige socio y fecha antes de enviar", async () => {
    renderModal();
    fireEvent.change(screen.getByLabelText(/Fecha de ingreso/), { target: { value: "" } });
    submit();
    expect(await screen.findByText("Seleccione un socio de negocio.")).toBeTruthy();
    expect(screen.getByText("Indique una fecha válida.")).toBeTruthy();
    expect(apiPost).not.toHaveBeenCalled();
  });

  it("muestra el mensaje oficial del servidor y no cierra", async () => {
    vi.mocked(apiPost).mockRejectedValue(
      apiError(409, "EMPLOYEE_ALREADY_EXISTS", "El tercero ya tiene una ficha."),
    );
    renderModal();
    await pickPartner();
    submit();
    expect(await screen.findByText("El tercero ya tiene una ficha.")).toBeTruthy();
    expect(onCreated).not.toHaveBeenCalled();
  });

  it("sin permiso de consulta de socios explica el bloqueo y no busca", () => {
    grant("employees.create");
    renderModal(false);
    expect(screen.getByText(/necesita el permiso de consulta de socios/)).toBeTruthy();
    expect(screen.queryByRole("combobox", { name: /Socio de negocio/ })).toBeNull();
    expect((screen.getByRole("button", { name: "Crear ficha" }) as HTMLButtonElement).disabled).toBe(true);
    expect(apiGet).not.toHaveBeenCalled();
  });

  it("sin consulta de catálogos no los pide y permite el alta mínima", async () => {
    grant(VIEW);
    renderModal(false);
    expect(screen.getByText(/pueden asignarse después/)).toBeTruthy();
    expect(urls().some((u) => u.startsWith("/api/v1/employees/"))).toBe(false);
    expect(screen.queryByRole("button", { name: /Registrar o asignar rol/ })).toBeNull();
    expect(screen.getByText(/quien administre Datos Maestros/)).toBeTruthy();
  });

  it("con cambios, cerrar pide confirmación y solo descarta si se confirma", async () => {
    vi.mocked(message.confirm).mockResolvedValueOnce(false).mockResolvedValueOnce(true);
    renderModal();
    await pickPartner();
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    expect(onClose).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
  });
});

describe("wizard de MasterData con rol Employee (D-1)", () => {
  it("filtra los tipos de identificación por Employee y asigna el rol a un socio existente", async () => {
    renderModal();
    openWizard();
    expect(screen.getByText("Nuevo Empleado")).toBeTruthy();
    await waitFor(() =>
      expect(urls().some((u) => u.includes("sri-id-types/by-usage/employee"))).toBe(true),
    );
    await searchInWizard();
    expect(await screen.findByText(/Asígnele el rol Empleado si aún no lo tiene/)).toBeTruthy();
    expect(screen.queryByText(/Tipo de Proveedor/)).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Asignar como Empleado" }));

    await waitFor(() => expect(partnerBox).toThrow());
    await waitFor(() => expect(screen.getByText("Ana Pérez")).toBeTruthy());
    expect(apiPost).toHaveBeenCalledExactlyOnceWith(`${PARTNERS}/bp-1/roles`, { roleType: 3 });
    submit();
    await waitFor(() =>
      expect(vi.mocked(apiPost).mock.calls.at(-1)?.[1]).toMatchObject({ businessPartnerId: "bp-1" }),
    );
  });

  it("no hereda textos de Clientes o Proveedores: sin RUC, razón social ni nombre comercial", async () => {
    renderModal();
    openWizard();
    await screen.findByRole("option", { name: /Cédula/ });
    expect(screen.getByLabelText(/Nombres y apellidos/, { selector: "input" })).toBeTruthy();
    expect(screen.queryByLabelText(/Nombre comercial/)).toBeNull();
    expect(screen.getByRole("dialog").innerHTML).not.toMatch(/RUC|raz[oó]n social|nombre comercial/i);
    const types = within(screen.getByLabelText(/Tipo de identificación/)).getAllByRole("option");
    expect(types.map((o) => (o as HTMLOptionElement).value)).toEqual(["05", "06"]);
    expect(urls().some((u) => u.includes("sri-id-types/by-usage/employee"))).toBe(true);
  });

  it("un socio con RUC no se puede asignar como empleado; cédula y pasaporte sí", async () => {
    partnerRows = [
      { ...BP, id: "bp-ruc", identificationType: "04", identificationNumber: "0102030400001", legalName: "Con RUC" },
      { ...BP, id: "bp-pas", identificationType: "06", identificationNumber: "AB123", legalName: "Con pasaporte" },
    ];
    renderModal();
    openWizard();
    await searchInWizard();
    expect(await screen.findByText(/no admite el rol Empleado/)).toBeTruthy();
    expect(screen.getAllByRole("button", { name: "Asignar como Empleado" })).toHaveLength(1);
    expect(apiPost).not.toHaveBeenCalled();
  });

  it("un RUC escrito como cédula no crea el socio ni asigna el rol", async () => {
    partnerRows = [];
    renderModal();
    openWizard();
    await screen.findByRole("option", { name: /Cédula/ });
    fireEvent.change(screen.getByPlaceholderText("Cédula, pasaporte o nombres…"), {
      target: { value: "0102030400001" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));
    await screen.findByText(/Completa los datos/);
    fireEvent.click(screen.getByRole("button", { name: /Crear Empleado/ }));
    expect(await screen.findByText(/Cédula inválida/)).toBeTruthy();
    expect(screen.getByText("Ingrese nombres y apellidos (entre 2 y 200 caracteres).")).toBeTruthy();
    expect(apiPost).not.toHaveBeenCalled();
  });

  it("crea el socio por el wizard oficial y le asigna el rol", async () => {
    partnerRows = [];
    vi.mocked(apiPost).mockResolvedValue({ ...BP });
    renderModal();
    openWizard();
    await screen.findByRole("option", { name: /Cédula/ });
    await searchInWizard();
    await screen.findByText(/Completa los datos para registrar un nuevo empleado/);
    fireEvent.change(screen.getByLabelText(/Nombres y apellidos/, { selector: "input" }), {
      target: { value: "Ana Pérez" },
    });
    fireEvent.click(screen.getByRole("button", { name: /Crear Empleado/ }));

    await waitFor(() => expect(apiPost).toHaveBeenCalledTimes(2));
    expect(vi.mocked(apiPost).mock.calls[0]).toEqual([
      PARTNERS,
      expect.objectContaining({
        identificationType: "05",
        identificationNumber: "0102030400",
        legalName: "Ana Pérez",
        tradeName: null,
      }),
    ]);
    expect(vi.mocked(apiPost).mock.calls[1]).toEqual([`${PARTNERS}/bp-1/roles`, { roleType: 3 }]);
    await waitFor(() => expect(screen.getByRole("button", { name: "Crear ficha" })).toBeTruthy());
  });

  it("sin permiso de creación no envía el alta del socio y lo explica", async () => {
    grant(VIEW, UPDATE);
    partnerRows = [];
    renderModal(false);
    openWizard();
    await screen.findByRole("option", { name: /Cédula/ });
    await searchInWizard();
    await screen.findByText(/Completa los datos/);
    fireEvent.change(screen.getByLabelText(/Nombres y apellidos/, { selector: "input" }), {
      target: { value: "Ana Pérez" },
    });
    fireEvent.click(screen.getByRole("button", { name: /Crear Empleado/ }));
    expect(await screen.findByText(/No tiene permiso para crear socios/)).toBeTruthy();
    expect(apiPost).not.toHaveBeenCalled();
  });

  it("muestra el rechazo del servidor si el rol ya está activo", async () => {
    vi.mocked(apiPost).mockRejectedValue(
      apiError(422, "VALIDATION", "El rol Employee ya está activo para este BusinessPartner."),
    );
    renderModal();
    openWizard();
    await searchInWizard();
    fireEvent.click(await screen.findByRole("button", { name: "Asignar como Empleado" }));
    expect(await screen.findByText(/ya está activo/)).toBeTruthy();
  });
});
