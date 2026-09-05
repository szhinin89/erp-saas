// @vitest-environment jsdom
/**
 * ZH-MASTERDATA-PARTNER-PROGRESSIVE-FORM-UX-02 — el wizard deja de ocultar el formulario
 * detrás de un paso previo de búsqueda: ahora se renderiza siempre desde el inicio, con los
 * campos deshabilitados hasta que el usuario busca sin resultados o elige "Crear nuevo
 * registro". Sin stepper, sin "Revisar antes de guardar", sin "Anterior"/"Continuar" — mismo
 * endpoint, payload, campos y validaciones que antes.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import {
  MasterDataPartnerWizard,
  getPartnerSearchResultState,
} from "./MasterDataPartnerWizard";
import { businessPartnerService } from "../api/businessPartnerService";
import { paymentTermService } from "../api/paymentTermService";
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

/**
 * ZH-MASTERDATA-PARTNER-SEARCH-ROLE-FLAGS-API-07: el backend expone isCustomer/isSupplier
 * (roles activos) y los CanAssignAs* derivados directamente en BusinessPartnerSummaryDto —
 * el fixture base ya no tiene ningún rol; los otros fixtures ajustan flags puntuales.
 */
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
  isCustomer: false,
  isSupplier: false,
  canAssignAsCustomer: true,
  canAssignAsSupplier: true,
};

/** ZH-MASTERDATA-PARTNER-FUNCTIONAL-CASE-MATRIX-06 — variantes por estado de rol. */
const CUSTOMER_ONLY_BP: BusinessPartnerSummaryDto = {
  ...FOUND_BP,
  id: "bp-customer-only",
  legalName: "Consumidor Final",
  isCustomer: true,
  isSupplier: false,
  canAssignAsCustomer: false,
  canAssignAsSupplier: true,
};
const SUPPLIER_ONLY_BP: BusinessPartnerSummaryDto = {
  ...FOUND_BP,
  id: "bp-supplier-only",
  legalName: "Proveedor Existente",
  isCustomer: false,
  isSupplier: true,
  canAssignAsCustomer: true,
  canAssignAsSupplier: false,
};
const BOTH_ROLES_BP: BusinessPartnerSummaryDto = {
  ...FOUND_BP,
  id: "bp-both-roles",
  legalName: "Tercero Mixto",
  isCustomer: true,
  isSupplier: true,
  canAssignAsCustomer: false,
  canAssignAsSupplier: false,
};
const NO_ROLE_BP: BusinessPartnerSummaryDto = {
  ...FOUND_BP,
  id: "bp-no-role",
  legalName: "Tercero Sin Rol",
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

describe("MasterDataPartnerWizard — formulario visible desde el inicio (sin stepper)", () => {
  it("Nuevo Cliente renderiza el formulario desde el estado inicial", () => {
    renderWizard({ role: "customer" });

    expect(screen.getByText("Datos principales del cliente")).toBeTruthy();
    expect(screen.getByText("Tipo de identificación")).toBeTruthy();
    expect(screen.getByText("Razón social")).toBeTruthy();
  });

  it("Nuevo Proveedor renderiza el formulario desde el estado inicial", () => {
    renderWizard({ role: "supplier" });

    expect(screen.getByText("Datos principales del proveedor")).toBeTruthy();
    expect(screen.getByText("Tipo de identificación")).toBeTruthy();
  });

  it("los campos del formulario están deshabilitados inicialmente", () => {
    renderWizard();

    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(true);
    expect(screen.getByLabelText(/^Razón social/i).matches(":disabled")).toBe(true);
  });

  it("el botón Crear cliente está deshabilitado inicialmente", () => {
    renderWizard({ role: "customer" });

    const btn = screen.getByRole("button", { name: /Crear Cliente/i }) as HTMLButtonElement;
    expect(btn.disabled).toBe(true);
  });

  it("no renderiza stepper ni pasos numerados", () => {
    renderWizard();

    expect(document.querySelector(".prd-wiz-progress")).toBeNull();
    expect(document.querySelector(".prd-wiz-step")).toBeNull();
    expect(screen.queryByText(/^Buscar y asignar$/)).toBeNull();
  });

  it('no muestra "Revisar antes de guardar" ni un bloque de resumen duplicado', () => {
    renderWizard();

    expect(screen.queryByText("Revisar antes de guardar")).toBeNull();
    expect(document.querySelector(".prd-review-grid")).toBeNull();
    expect(screen.getAllByText("Tipo de identificación")).toHaveLength(1);
  });

  it('no muestra botones "Anterior" ni "Continuar"', () => {
    renderWizard();

    expect(screen.queryByText(/^Anterior$/)).toBeNull();
    expect(screen.queryByText(/^Continuar$/)).toBeNull();
  });

  it("muestra la ayuda de duplicados (notice) antes de buscar, sin párrafos largos sueltos", () => {
    renderWizard();

    expect(
      screen.getByText(/Busca primero para evitar duplicados/i),
    ).toBeTruthy();
  });
});

describe("MasterDataPartnerWizard — buscador integrado en la cabecera del card (ZH-MASTERDATA-PARTNER-HEADER-SEARCH-UX-04E)", () => {
  it("Cliente renderiza el título de cabecera \"Nuevo cliente\"", () => {
    renderWizard({ role: "customer" });

    expect(document.querySelector(".pg-section-label")?.textContent).toBe(
      "Nuevo Cliente",
    );
  });

  it("Proveedor renderiza el título de cabecera \"Nuevo proveedor\"", () => {
    renderWizard({ role: "supplier" });

    expect(document.querySelector(".pg-section-label")?.textContent).toBe(
      "Nuevo Proveedor",
    );
  });

  it("el input de búsqueda está dentro de la cabecera del card (.md-partner-card-header)", () => {
    renderWizard();

    const header = document.querySelector(".md-partner-card-header");
    expect(header).toBeTruthy();
    expect(header?.querySelector("input.prd-search-input")).toBeTruthy();
  });

  it('el botón "Buscar" está dentro de la cabecera del card', () => {
    renderWizard();

    const header = document.querySelector(".md-partner-card-header");
    const buscarBtn = Array.from(header?.querySelectorAll("button") ?? []).find(
      (b) => b.textContent === "Buscar",
    );
    expect(buscarBtn).toBeTruthy();
  });

  it('el botón "Crear nuevo registro" está dentro de la cabecera del card y no suelto debajo', () => {
    renderWizard();

    const buttons = screen.getAllByRole("button", {
      name: /Crear nuevo registro/i,
    });
    expect(buttons).toHaveLength(1);
    expect(buttons[0].closest(".md-partner-card-header")).toBeTruthy();
  });

  it('no existe un bloque independiente "Buscar cliente existente" debajo del header', () => {
    renderWizard({ role: "customer" });

    expect(screen.queryByText("Buscar cliente existente")).toBeNull();
    expect(document.querySelector(".md-partner-search-wrap")).toBeNull();
  });

  it('no existe un bloque independiente "Buscar proveedor existente" debajo del header', () => {
    renderWizard({ role: "supplier" });

    expect(screen.queryByText("Buscar proveedor existente")).toBeNull();
    expect(document.querySelector(".md-partner-search-wrap")).toBeNull();
  });

  it("Cliente muestra la descripción general debajo del título, dentro de la cabecera", () => {
    renderWizard({ role: "customer" });

    const header = document.querySelector(".md-partner-card-header");
    const titleBlock = header?.querySelector(".md-partner-card-title-block");
    expect(titleBlock).toBeTruthy();
    expect(titleBlock?.querySelector(".pg-section-label")).toBeTruthy();
    expect(
      titleBlock?.querySelector(".md-partner-card-description")?.textContent,
    ).toMatch(/ventas, facturación y cuentas por cobrar/i);
  });

  it("Proveedor muestra la descripción general debajo del título, dentro de la cabecera", () => {
    renderWizard({ role: "supplier" });

    const header = document.querySelector(".md-partner-card-header");
    const titleBlock = header?.querySelector(".md-partner-card-title-block");
    expect(
      titleBlock?.querySelector(".md-partner-card-description")?.textContent,
    ).toMatch(/compras, gastos, retenciones y cuentas por pagar/i);
  });

  it("no queda un párrafo de descripción suelto en el body (pg-section-body)", () => {
    renderWizard();

    const body = document.querySelector(".pg-section-body");
    expect(body?.querySelector(".md-partner-card-description")).toBeNull();
    expect(body?.querySelector(".md-partner-intro")).toBeNull();
  });

  it("el notice/help inicial se renderiza dentro de .md-partner-header-search (cabecera), no en el body", () => {
    renderWizard();

    const headerSearch = document.querySelector(".md-partner-header-search");
    const body = document.querySelector(".pg-section-body");
    const noticeBox = document.querySelector(".md-partner-search-notice");
    expect(headerSearch?.contains(noticeBox)).toBe(true);
    expect(noticeBox?.querySelector(".zh-page-notice")).toBeTruthy();
    expect(body?.querySelector(".md-partner-search-notice")).toBeNull();
    // La única notice permitida en el body es la del final del formulario
    // (.md-partner-final-notice) — la de búsqueda/ayuda no debe estar aquí.
    expect(body?.querySelector(".zh-page-notice:not(.md-partner-final-notice)")).toBeNull();
  });

  it('"No se encontró" reemplaza la ayuda inicial dentro de .md-partner-search-notice', async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([]);
    renderWizard({ role: "customer" });

    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "Empresa Nueva" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));

    await waitFor(() => {
      const headerSearch = document.querySelector(".md-partner-header-search");
      const noticeBox = document.querySelector(".md-partner-search-notice");
      expect(headerSearch?.contains(noticeBox)).toBe(true);
      expect(noticeBox?.textContent).toMatch(/No se encontró "Empresa Nueva"/i);
      expect(noticeBox?.textContent).not.toMatch(/Busca primero para evitar duplicados/i);
    });
    expect(
      document
        .querySelector(".pg-section-body")
        ?.querySelector(".zh-page-notice:not(.md-partner-final-notice)"),
    ).toBeNull();
  });

  it("error técnico reemplaza la ayuda inicial, no habilita el formulario y usa formatApiRequestError", async () => {
    vi.mocked(businessPartnerService.search).mockRejectedValue(new Error("network down"));
    renderWizard();

    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "0999999999" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));

    await waitFor(() => {
      const headerSearch = document.querySelector(".md-partner-header-search");
      const noticeBox = document.querySelector(".md-partner-search-notice");
      expect(headerSearch?.contains(noticeBox)).toBe(true);
      expect(noticeBox?.querySelector('[role="alert"]')).toBeTruthy();
    });
    expect(
      document.querySelector(".md-partner-search-notice")?.textContent,
    ).not.toMatch(/Busca primero para evitar duplicados/i);
    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(true);
  });

  it("solo un notice está visible a la vez dentro de .md-partner-search-notice (estado inicial)", () => {
    renderWizard();

    const noticeBox = document.querySelector(".md-partner-search-notice");
    expect(noticeBox?.querySelectorAll(".zh-page-notice").length).toBe(1);
  });

  it('"Crear nuevo registro" en la fila sigue habilitando el formulario en el mismo tab', () => {
    renderWizard();

    fireEvent.click(
      screen.getByRole("button", { name: /Crear nuevo registro/i }),
    );

    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(false);
  });

  it("Cliente y Proveedor usan exactamente la misma composición de clases", () => {
    const { unmount } = renderWizard({ role: "customer" });
    const customerHasHeaderSearch = !!document.querySelector(
      ".md-partner-header-search",
    );
    const customerHasNotice = !!document.querySelector(".md-partner-search-notice");
    unmount();

    renderWizard({ role: "supplier" });
    expect(!!document.querySelector(".md-partner-header-search")).toBe(
      customerHasHeaderSearch,
    );
    expect(!!document.querySelector(".md-partner-search-notice")).toBe(
      customerHasNotice,
    );
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

  it("búsqueda sin resultados muestra mensaje contextual y habilita los campos", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([]);
    renderWizard({ role: "customer" });

    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "Empresa Nueva" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));

    await waitFor(() =>
      expect(screen.getByText(/No se encontró "Empresa Nueva"/i)).toBeTruthy(),
    );
    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(false);
  });

  it("búsqueda con resultados muestra la acción Asignar como cliente y no prioriza crear duplicado", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([FOUND_BP]);
    renderWizard({ role: "customer" });

    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "0999999999" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));

    await waitFor(() =>
      expect(screen.getByRole("button", { name: /Asignar como Cliente/i })).toBeTruthy(),
    );
    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(true);
    expect(screen.getByRole("button", { name: /Crear nuevo registro/i })).toBeTruthy();
  });

  it('"Crear nuevo registro" habilita el formulario en el mismo tab', () => {
    renderWizard();

    expect(screen.getByLabelText(/^Razón social/i)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: /Crear nuevo registro/i }));

    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(false);
    expect(screen.getByText(/Datos principales del cliente/i)).toBeTruthy();
  });
});

describe("MasterDataPartnerWizard — auditoría de estado (ZH-MASTERDATA-PARTNER-FLOW-STATE-AUDIT-05)", () => {
  it('"Crear nuevo registro" no llama onSubmitCreate ni onAssignRole — solo habilita el formulario', () => {
    const onSubmitCreate = vi.fn().mockResolvedValue(undefined);
    const onAssignRole = vi.fn().mockResolvedValue(undefined);
    renderWizard({ onSubmitCreate, onAssignRole });

    fireEvent.click(screen.getByRole("button", { name: /Crear nuevo registro/i }));

    expect(onSubmitCreate).not.toHaveBeenCalled();
    expect(onAssignRole).not.toHaveBeenCalled();
  });

  it("una nueva búsqueda limpia los resultados previos antes de resolver (no deja resultados obsoletos si la nueva búsqueda falla)", async () => {
    vi.mocked(businessPartnerService.search)
      .mockResolvedValueOnce([FOUND_BP])
      .mockRejectedValueOnce(new Error("network down"));
    renderWizard();

    // 1ª búsqueda: encuentra un resultado.
    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "0999999999" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));
    await waitFor(() => expect(screen.getByText("Empresa Existente")).toBeTruthy());

    // 2ª búsqueda: falla técnicamente — el resultado anterior no debe seguir visible
    // junto al error (evita asignar sobre datos obsoletos).
    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "otra query" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));

    await waitFor(() => {
      expect(document.querySelector(".md-partner-search-notice")?.querySelector('[role="alert"]')).toBeTruthy();
    });
    expect(screen.queryByText("Empresa Existente")).toBeNull();
    expect(document.querySelector(".md-search-results")).toBeNull();
  });

  it("los mensajes de error de asignar/guardar usan formatApiRequestError con fallback vía i18n (no texto hardcodeado ajeno a t())", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([FOUND_BP]);
    const onAssignRole = vi.fn().mockRejectedValue(new Error("boom"));
    renderWizard({ onAssignRole });

    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "0999999999" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));
    await waitFor(() =>
      expect(screen.getByRole("button", { name: /Asignar como Cliente/i })).toBeTruthy(),
    );

    fireEvent.click(screen.getByRole("button", { name: /Asignar como Cliente/i }));

    await waitFor(() => {
      const noticeBox = document.querySelector(".md-partner-search-notice");
      expect(noticeBox?.textContent).toMatch(/Error al asignar el rol\.|boom/i);
    });
  });
});

describe("MasterDataPartnerWizard — máquina de estados de mensajes (ZH-MASTERDATA-PARTNER-MESSAGE-ARCHITECTURE-FIX-03)", () => {
  it("Cliente: estado inicial muestra el mensaje informativo, sin error ni fieldset habilitado", () => {
    renderWizard({ role: "customer" });

    expect(
      screen.getByText(/Busca primero para evitar duplicados/i),
    ).toBeTruthy();
    expect(screen.queryByText(/Error al buscar/i)).toBeNull();
    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(true);
  });

  it("Proveedor: estado inicial muestra el mensaje informativo, sin error ni fieldset habilitado", () => {
    renderWizard({ role: "supplier" });

    expect(
      screen.getByText(/Busca primero para evitar duplicados/i),
    ).toBeTruthy();
    expect(screen.queryByText(/Error al buscar/i)).toBeNull();
    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(true);
  });

  it("Cliente: búsqueda sin resultados muestra 'No se encontró' y NO muestra 'Error al buscar' ni el mensaje inicial", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([]);
    renderWizard({ role: "customer" });

    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "Empresa Nueva" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));

    await waitFor(() =>
      expect(
        screen.getByText(
          'No se encontró "Empresa Nueva". Completa los datos para registrar un nuevo cliente.',
        ),
      ).toBeTruthy(),
    );
    expect(screen.queryByText(/Error al buscar/i)).toBeNull();
    expect(
      screen.queryByText(/Busca primero para evitar duplicados/i),
    ).toBeNull();
    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(false);
  });

  it("Proveedor: búsqueda sin resultados muestra 'No se encontró' y NO muestra 'Error al buscar' ni el mensaje inicial", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([]);
    renderWizard({ role: "supplier" });

    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "Proveedor Nuevo" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));

    await waitFor(() =>
      expect(
        screen.getByText(
          'No se encontró "Proveedor Nuevo". Completa los datos para registrar un nuevo proveedor.',
        ),
      ).toBeTruthy(),
    );
    expect(screen.queryByText(/Error al buscar/i)).toBeNull();
    expect(
      screen.queryByText(/Busca primero para evitar duplicados/i),
    ).toBeNull();
    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(false);
  });

  it("Cliente: error técnico de búsqueda usa formatApiRequestError, no habilita el formulario y no muestra 'No se encontró'", async () => {
    vi.mocked(businessPartnerService.search).mockRejectedValue(new Error("network down"));
    renderWizard({ role: "customer" });

    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "0999999999" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toBeTruthy(),
    );
    expect(screen.queryByText(/No se encontró/i)).toBeNull();
    expect(
      screen.queryByText(/Busca primero para evitar duplicados/i),
    ).toBeNull();
    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(true);
  });

  it("Proveedor: error técnico de búsqueda usa formatApiRequestError, no habilita el formulario y no muestra 'No se encontró'", async () => {
    vi.mocked(businessPartnerService.search).mockRejectedValue(new Error("network down"));
    renderWizard({ role: "supplier" });

    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "0999999999" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toBeTruthy(),
    );
    expect(screen.queryByText(/No se encontró/i)).toBeNull();
    expect(
      screen.queryByText(/Busca primero para evitar duplicados/i),
    ).toBeNull();
    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(true);
  });

  it("Proveedor: búsqueda con resultados muestra 'Asignar como Proveedor' igual que Cliente muestra 'Asignar como Cliente'", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([FOUND_BP]);
    renderWizard({ role: "supplier" });

    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "0999999999" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));

    await waitFor(() =>
      expect(screen.getByRole("button", { name: /Asignar como Proveedor/i })).toBeTruthy(),
    );
  });
});

describe("MasterDataPartnerWizard — formulario sin resumen repetido", () => {
  it("Nuevo Cliente conserva los campos y sigue mostrando 'Datos principales del cliente'", () => {
    renderWizard({ role: "customer" });
    fireEvent.click(screen.getByRole("button", { name: /Crear nuevo registro/i }));

    expect(screen.getByText("Datos principales del cliente")).toBeTruthy();
    expect(screen.getByText("Tipo de identificación")).toBeTruthy();
    expect(screen.getByText("Número de identificación")).toBeTruthy();
    expect(screen.getByText("Razón social")).toBeTruthy();
  });

  it("Nuevo Proveedor conserva los campos y sigue mostrando 'Datos principales del proveedor'", () => {
    renderWizard({ role: "supplier" });
    fireEvent.click(screen.getByRole("button", { name: /Crear nuevo registro/i }));

    expect(screen.getByText("Datos principales del proveedor")).toBeTruthy();
    expect(screen.getByText("Tipo de identificación")).toBeTruthy();
  });

  it("mantiene el aviso informativo final de asignación de rol", () => {
    renderWizard({ role: "customer" });
    fireEvent.click(screen.getByRole("button", { name: /Crear nuevo registro/i }));

    expect(
      screen.getByText(
        "Quedará disponible para ventas, facturación y cuentas por cobrar.",
      ),
    ).toBeTruthy();
  });

  it("mantiene el estado loading/disabled del botón de guardar", () => {
    renderWizard({ submitting: true });
    fireEvent.click(screen.getByRole("button", { name: /Crear nuevo registro/i }));

    const btn = screen.getByRole("button", { name: /Guardando/i }) as HTMLButtonElement;
    expect(btn.disabled).toBe(true);
  });
});

describe("MasterDataPartnerWizard — guardar conserva payload actual", () => {
  it("Guardar cliente envía el mismo payload de creación (sin campos de revisión nuevos)", async () => {
    const onSubmitCreate = vi.fn().mockResolvedValue(undefined);
    renderWizard({ role: "customer", onSubmitCreate });
    fireEvent.click(screen.getByRole("button", { name: /Crear nuevo registro/i }));

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

  it("Guardar proveedor envía el mismo payload de creación (sin campos de revisión nuevos)", async () => {
    vi.mocked(paymentTermService.list).mockResolvedValue([
      {
        id: "pt-1",
        code: "30D",
        name: "30 días",
        installments: 1,
        daysBetweenInstallments: 30,
        totalDays: 30,
        summary: "30 días",
        isActive: true,
      },
    ]);
    const onSubmitCreate = vi.fn().mockResolvedValue(undefined);
    renderWizard({ role: "supplier", onSubmitCreate });
    fireEvent.click(screen.getByRole("button", { name: /Crear nuevo registro/i }));

    fireEvent.change(screen.getByLabelText(/Número de identificación/i), {
      target: { value: "1710034065001" },
    });
    fireEvent.change(screen.getByLabelText(/^Razón social/i), {
      target: { value: "Proveedor de prueba" },
    });
    fireEvent.change(screen.getByLabelText(/Tipo de Proveedor/i), {
      target: { value: "01" },
    });
    await waitFor(() =>
      expect(screen.getByLabelText(/Condición de pago/i).querySelector('option[value="pt-1"]')).toBeTruthy(),
    );
    fireEvent.change(screen.getByLabelText(/Condición de pago/i), {
      target: { value: "pt-1" },
    });

    fireEvent.click(screen.getByRole("button", { name: /Crear Proveedor/i }));

    await waitFor(() => expect(onSubmitCreate).toHaveBeenCalled());
    const [body, supplierConfig] = onSubmitCreate.mock.calls[0];
    expect(body).toEqual({
      identificationType: "04",
      identificationNumber: "1710034065001",
      legalEntityTypeCode: undefined,
      legalName: "Proveedor de prueba",
      tradeName: null,
      countryCode: "EC",
    });
    expect(supplierConfig).toEqual({
      refundProviderTypeCode: "01",
      paymentTermId: "pt-1",
    });
  });
});

describe("MasterDataPartnerWizard — edición y modo embebido no bloqueados por búsqueda", () => {
  it("edición: no exige búsqueda previa y muestra directamente el formulario habilitado con datos precargados", () => {
    renderWizard({
      editingPartner: FOUND_BP,
    });

    expect(screen.queryByText(/^Buscar (cliente|proveedor) existente$/)).toBeNull();
    expect(screen.getByText("Editar datos principales")).toBeTruthy();
    expect(screen.getByDisplayValue("Empresa Existente")).toBeTruthy();
    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(false);
  });

  it("modo embedded con initialValues: sigue renderizando el formulario habilitado sin bloque de búsqueda", () => {
    renderWizard({
      embedded: true,
      initialValues: {
        identificationType: "04",
        identificationNumber: "1710034065001",
        legalName: "Proveedor Precargado",
      },
    });

    expect(screen.queryByText(/^Buscar (cliente|proveedor) existente$/)).toBeNull();
    expect(screen.getByDisplayValue("Proveedor Precargado")).toBeTruthy();
    const fieldset = document.querySelector(
      "fieldset.md-partner-fieldset",
    ) as HTMLFieldSetElement;
    expect(fieldset.disabled).toBe(false);
    expect(screen.queryByText(/Guardar borrador/i)).toBeNull();
  });
});

describe("getPartnerSearchResultState — helper puro (ZH-MASTERDATA-PARTNER-FUNCTIONAL-CASE-MATRIX-06 §9)", () => {
  function bpWithFlags(isCustomer: boolean, isSupplier: boolean): BusinessPartnerSummaryDto {
    return { ...FOUND_BP, isCustomer, isSupplier };
  }

  it("sin ningún rol → canAssignNoRole", () => {
    expect(
      getPartnerSearchResultState(bpWithFlags(false, false), "customer"),
    ).toBe("canAssignNoRole");
  });

  it("target=customer, solo tiene supplier → canAssignOtherRole", () => {
    expect(
      getPartnerSearchResultState(bpWithFlags(false, true), "customer"),
    ).toBe("canAssignOtherRole");
  });

  it("target=customer, ya tiene customer → alreadyTarget", () => {
    expect(
      getPartnerSearchResultState(bpWithFlags(true, false), "customer"),
    ).toBe("alreadyTarget");
  });

  it("target=customer, tiene ambos roles → alreadyBoth", () => {
    expect(
      getPartnerSearchResultState(bpWithFlags(true, true), "customer"),
    ).toBe("alreadyBoth");
  });

  it("target=supplier, solo tiene customer → canAssignOtherRole", () => {
    expect(
      getPartnerSearchResultState(bpWithFlags(true, false), "supplier"),
    ).toBe("canAssignOtherRole");
  });

  it("target=supplier, ya tiene supplier → alreadyTarget", () => {
    expect(
      getPartnerSearchResultState(bpWithFlags(false, true), "supplier"),
    ).toBe("alreadyTarget");
  });

  it("target=supplier, tiene ambos roles → alreadyBoth", () => {
    expect(
      getPartnerSearchResultState(bpWithFlags(true, true), "supplier"),
    ).toBe("alreadyBoth");
  });
});

describe("MasterDataPartnerWizard — matriz de casos por rol del resultado (ZH-MASTERDATA-PARTNER-FUNCTIONAL-CASE-MATRIX-06)", () => {
  async function search(role: "customer" | "supplier", query = "0999999999") {
    renderWizard({ role });
    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: query },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));
    await waitFor(() => expect(document.querySelector(".md-search-results")).toBeTruthy());
  }

  it("Nuevo Cliente + resultado ya cliente (caso Consumidor Final): NO muestra 'Asignar como Cliente' y muestra 'Ya está registrado como cliente'", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([CUSTOMER_ONLY_BP]);
    await search("customer");

    expect(screen.getByText("Consumidor Final")).toBeTruthy();
    expect(
      screen.queryByRole("button", { name: /Asignar como Cliente/i }),
    ).toBeNull();
    expect(
      screen.getByText("Ya está registrado como cliente."),
    ).toBeTruthy();
  });

  it("Nuevo Cliente + resultado solo proveedor: muestra 'Asignar como Cliente' y 'Existe como proveedor'", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([SUPPLIER_ONLY_BP]);
    await search("customer");

    expect(
      screen.getByRole("button", { name: /Asignar como Cliente/i }),
    ).toBeTruthy();
    expect(screen.getByText("Existe como proveedor.")).toBeTruthy();
  });

  it("Nuevo Cliente + resultado cliente+proveedor: NO muestra 'Asignar como Cliente' y muestra 'cliente y proveedor'", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([BOTH_ROLES_BP]);
    await search("customer");

    expect(
      screen.queryByRole("button", { name: /Asignar como Cliente/i }),
    ).toBeNull();
    expect(
      screen.getByText("Ya está registrado como cliente y proveedor."),
    ).toBeTruthy();
  });

  it("Nuevo Cliente + resultado sin ningún rol: muestra 'Asignar como Cliente' y 'sin rol cliente'", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([NO_ROLE_BP]);
    await search("customer");

    expect(
      screen.getByRole("button", { name: /Asignar como Cliente/i }),
    ).toBeTruthy();
    expect(
      screen.getByText("Registro encontrado sin rol cliente."),
    ).toBeTruthy();
  });

  it("Nuevo Proveedor + resultado ya proveedor: NO muestra 'Asignar como Proveedor' y muestra 'Ya está registrado como proveedor'", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([SUPPLIER_ONLY_BP]);
    await search("supplier");

    expect(
      screen.queryByRole("button", { name: /Asignar como Proveedor/i }),
    ).toBeNull();
    expect(
      screen.getByText("Ya está registrado como proveedor."),
    ).toBeTruthy();
  });

  it("Nuevo Proveedor + resultado solo cliente: muestra 'Asignar como Proveedor' y 'Existe como cliente'", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([CUSTOMER_ONLY_BP]);
    await search("supplier");

    expect(
      screen.getByRole("button", { name: /Asignar como Proveedor/i }),
    ).toBeTruthy();
    expect(screen.getByText("Existe como cliente.")).toBeTruthy();
  });

  it("Nuevo Proveedor + resultado cliente+proveedor: NO muestra 'Asignar como Proveedor'", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([BOTH_ROLES_BP]);
    await search("supplier");

    expect(
      screen.queryByRole("button", { name: /Asignar como Proveedor/i }),
    ).toBeNull();
    expect(
      screen.getByText("Ya está registrado como cliente y proveedor."),
    ).toBeTruthy();
  });

  it("click en resultado ya cliente no puede llamar onAssignRole (no hay botón para ese resultado)", async () => {
    const onAssignRole = vi.fn().mockResolvedValue(undefined);
    vi.mocked(businessPartnerService.search).mockResolvedValue([CUSTOMER_ONLY_BP]);
    renderWizard({ role: "customer", onAssignRole });
    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "0999999999" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));
    await waitFor(() => expect(screen.getByText("Consumidor Final")).toBeTruthy());

    expect(onAssignRole).not.toHaveBeenCalled();
  });

  it("click en resultado con rol faltante sí llama onAssignRole", async () => {
    const onAssignRole = vi.fn().mockResolvedValue(undefined);
    vi.mocked(businessPartnerService.search).mockResolvedValue([SUPPLIER_ONLY_BP]);
    renderWizard({ role: "customer", onAssignRole });
    fireEvent.change(screen.getByPlaceholderText(/RUC, cédula o razón social/i), {
      target: { value: "0999999999" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Buscar" }));
    fireEvent.click(
      await screen.findByRole("button", { name: /Asignar como Cliente/i }),
    );

    await waitFor(() => expect(onAssignRole).toHaveBeenCalledWith(SUPPLIER_ONLY_BP.id));
  });

  it("resultados múltiples se evalúan de forma independiente (uno ya cliente, otro sin rol)", async () => {
    vi.mocked(businessPartnerService.search).mockResolvedValue([
      CUSTOMER_ONLY_BP,
      NO_ROLE_BP,
    ]);
    await search("customer");

    expect(screen.getByText("Ya está registrado como cliente.")).toBeTruthy();
    expect(screen.getByText("Registro encontrado sin rol cliente.")).toBeTruthy();
    expect(
      screen.getAllByRole("button", { name: /Asignar como Cliente/i }),
    ).toHaveLength(1);
  });
});
