// @vitest-environment jsdom
/**
 * ZH-MASTERDATA-PARTNER-FORM-UX-01 — el stepper numerado de 3 pasos se elimina visualmente;
 * el wizard pasa a mostrarse como un card único por secciones (Buscar → Identificación/
 * Config. comercial), sin cambiar endpoint, payload, campos ni validaciones.
 *
 * ZH-MASTERDATA-PARTNER-FORM-UX-01B — se elimina además la sección "Revisar antes de
 * guardar": al no existir wizard por pasos, repetía los mismos datos recién ingresados en
 * el formulario (aumentaba scroll y confundía). El aviso informativo final ("Al guardar
 * quedará disponible como {rol}") se conserva al pie de la sección de identificación.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataPartnerWizard } from "./MasterDataPartnerWizard";
import { businessPartnerService } from "../api/businessPartnerService";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";

vi.mock("../api/businessPartnerService", () => ({
  businessPartnerService: {
    search: vi.fn(),
  },
}));

vi.mock("../api/useSriIdTypes", () => ({
  useSriIdTypes: () => ({ options: [{ code: "05", name: "Cédula" }], loading: false }),
  useSriIdTypesByUsage: () => ({ options: [{ code: "05", name: "Cédula" }], loading: false }),
}));

vi.mock("../api/useLegalEntityTypes", () => ({
  useLegalEntityTypes: () => ({ options: [{ code: 1, name: "Persona natural" }], loading: false }),
}));

vi.mock("../api/useSriSupplierTypes", () => ({
  useSriSupplierTypes: () => ({ options: [{ code: "01", name: "Bienes" }], loading: false }),
}));

vi.mock("../api/paymentTermService", () => ({
  paymentTermService: {
    list: vi.fn().mockResolvedValue([]),
  },
}));

const FOUND_BP: BusinessPartnerSummaryDto = {
  id: "bp-1",
  identificationType: "05",
  identificationNumber: "0999999999",
  legalName: "Empresa Existente",
  tradeName: null,
  legalEntityTypeCode: 1,
  countryCode: "EC",
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
};

function baseProps(overrides: Partial<Parameters<typeof MasterDataPartnerWizard>[0]> = {}) {
  return {
    role: "customer" as const,
    draftKey: "test.draft.key",
    submitting: false,
    editingPartner: null,
    onSubmitCreate: vi.fn().mockResolvedValue(undefined),
    onSubmitUpdate: vi.fn().mockResolvedValue(undefined),
    onAssignRole: vi.fn().mockResolvedValue(undefined),
    onCancel: vi.fn(),
    ...overrides,
  };
}

function renderWizard(overrides: Partial<Parameters<typeof MasterDataPartnerWizard>[0]> = {}) {
  return render(
    <I18nProvider>
      <MasterDataPartnerWizard {...baseProps(overrides)} />
    </I18nProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
});

afterEach(() => {
  cleanup();
});

describe("MasterDataPartnerWizard — sin stepper visual", () => {
  it("Nuevo Cliente no renderiza la barra de pasos numerados", () => {
    renderWizard({ role: "customer" });

    expect(screen.queryByText("1")).toBeNull();
    expect(screen.queryByText(/^Buscar y asignar$/)).toBeNull();
    expect(screen.queryByText(/^Revisar y guardar$/)).toBeNull();
    expect(document.querySelector(".prd-wiz-progress")).toBeNull();
    expect(document.querySelector(".prd-wiz-step")).toBeNull();
  });

  it("Nuevo Proveedor no renderiza la barra de pasos numerados", () => {
    renderWizard({ role: "supplier" });

    expect(document.querySelector(".prd-wiz-progress")).toBeNull();
    expect(document.querySelector(".prd-wiz-step")).toBeNull();
  });

  it("Nuevo Cliente muestra el header del card y la descripción contextual", () => {
    renderWizard({ role: "customer" });

    expect(screen.getByText("Nuevo Cliente")).toBeTruthy();
    expect(
      screen.getByText(/ventas, facturación y cuentas por cobrar/i),
    ).toBeTruthy();
  });

  it("Nuevo Proveedor muestra el header del card y la descripción contextual", () => {
    renderWizard({ role: "supplier" });

    expect(screen.getByText("Nuevo Proveedor")).toBeTruthy();
    expect(
      screen.getByText(/compras, gastos, retenciones y cuentas por pagar/i),
    ).toBeTruthy();
  });

  it('muestra el bloque "¿Ya existe en el sistema?"', () => {
    renderWizard();

    expect(screen.getByText("¿Ya existe en el sistema?")).toBeTruthy();
    expect(
      screen.getByText(/Busca por RUC, cédula o razón social para evitar duplicados/i),
    ).toBeTruthy();
  });
});

describe("MasterDataPartnerWizard — búsqueda conserva comportamiento actual", () => {
  it("busca con el servicio existente y no rompe el endpoint/comportamiento", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([FOUND_BP]);
    renderWizard();

    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "0999999999" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));

    await waitFor(() =>
      expect(businessPartnerService.search).toHaveBeenCalledWith({
        q: "0999999999",
        take: 10,
      }),
    );
    await waitFor(() => expect(screen.getByText("Empresa Existente")).toBeTruthy());
  });

  it('"Crear sin buscar" conserva el comportamiento actual: pasa directo al formulario', () => {
    renderWizard();

    expect(screen.queryByLabelText(/Tipo de identificación/i)).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: /Crear sin buscar/i }));

    expect(screen.getByText(/Datos principales del cliente/i)).toBeTruthy();
  });
});

describe("MasterDataPartnerWizard — formulario sin resumen repetido (ZH-MASTERDATA-PARTNER-FORM-UX-01B)", () => {
  it("Nuevo Cliente conserva los campos y sigue mostrando 'Datos principales del cliente'", () => {
    renderWizard({ role: "customer" });
    fireEvent.click(screen.getByRole("button", { name: /Crear sin buscar/i }));

    expect(screen.getByText("Datos principales del cliente")).toBeTruthy();
    expect(screen.getByText("Tipo de identificación")).toBeTruthy();
    expect(screen.getByText("Número de identificación")).toBeTruthy();
    expect(screen.getByText("Razón social")).toBeTruthy();
  });

  it("Nuevo Proveedor conserva los campos y sigue mostrando 'Datos principales del proveedor'", () => {
    renderWizard({ role: "supplier" });
    fireEvent.click(screen.getByRole("button", { name: /Crear sin buscar/i }));

    expect(screen.getByText("Datos principales del proveedor")).toBeTruthy();
    expect(screen.getByText("Tipo de identificación")).toBeTruthy();
  });

  it("no muestra 'Revisar antes de guardar' ni un bloque de resumen duplicado", () => {
    renderWizard();
    fireEvent.click(screen.getByRole("button", { name: /Crear sin buscar/i }));

    expect(screen.queryByText("Revisar antes de guardar")).toBeNull();
    expect(document.querySelector(".prd-review-grid")).toBeNull();
    // Cada dato del formulario aparece una sola vez — no hay resumen que lo repita.
    expect(screen.getAllByText("Tipo de identificación")).toHaveLength(1);
    expect(screen.getAllByText("Razón social")).toHaveLength(1);
  });

  it("mantiene el aviso informativo final de asignación de rol", () => {
    renderWizard({ role: "customer" });
    fireEvent.click(screen.getByRole("button", { name: /Crear sin buscar/i }));

    expect(
      screen.getByText((_, node) => node?.textContent === "Al guardar quedará disponible como cliente."),
    ).toBeTruthy();
  });

  it("mantiene el estado loading/disabled del botón de guardar", () => {
    renderWizard({ submitting: true });
    fireEvent.click(screen.getByRole("button", { name: /Crear sin buscar/i }));

    const btn = screen.getByRole("button", { name: /Guardando/i }) as HTMLButtonElement;
    expect(btn.disabled).toBe(true);
  });
});

describe("MasterDataPartnerWizard — guardar conserva payload actual", () => {
  it("Guardar cliente envía el mismo payload de creación (sin campos de revisión nuevos)", async () => {
    const onSubmitCreate = vi.fn().mockResolvedValue(undefined);
    renderWizard({ role: "customer", onSubmitCreate });
    fireEvent.click(screen.getByRole("button", { name: /Crear sin buscar/i }));

    // RUC válido (dígito verificador real) — el default de identificationType es "04" (RUC).
    fireEvent.change(screen.getByLabelText(/Número de identificación/i), {
      target: { value: "1710034065001" },
    });
    fireEvent.change(screen.getByLabelText(/^Razón social/i), {
      target: { value: "Cliente de prueba" },
    });
    fireEvent.click(screen.getByRole("button", { name: /Crear Cliente/i }));

    await waitFor(() => expect(onSubmitCreate).toHaveBeenCalled());
    const [body] = onSubmitCreate.mock.calls[0];
    expect(body).toEqual({
      identificationType: "04",
      identificationNumber: "1710034065001",
      legalEntityTypeCode: undefined,
      legalName: "Cliente de prueba",
      tradeName: null,
      countryCode: "EC",
    });
  });
});

describe("MasterDataPartnerWizard — no crea rutas ni toca servicios backend nuevos", () => {
  it("edición: salta la búsqueda y muestra directamente el formulario con datos precargados", () => {
    renderWizard({
      editingPartner: FOUND_BP,
    });

    expect(screen.queryByText("¿Ya existe en el sistema?")).toBeNull();
    expect(screen.getByText("Editar datos principales")).toBeTruthy();
    expect(screen.getByDisplayValue("Empresa Existente")).toBeTruthy();
  });
});
