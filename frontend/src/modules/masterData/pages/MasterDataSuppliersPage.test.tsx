// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataSuppliersPage } from "./MasterDataSuppliersPage";
import { useMasterDataSuppliersUiStore } from "../store/masterDataPartnerUiStore";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { paymentTermService } from "../api/paymentTermService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import type {
  BusinessPartnerRoleDto,
  BusinessPartnerSummaryDto,
} from "../types/businessPartner.types";

/**
 * CRITICAL-CONFIRMATIONS-BUSINESS-PARTNERS-04 — "Activar/desactivar proveedor" sigue el mismo
 * estándar que cliente (mismo bug de falso éxito, misma corrección: el hook relanza el error).
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    searchBusinessPartnersPaged: vi.fn(),
    deactivateBusinessPartner: vi.fn(),
    activateBusinessPartner: vi.fn(),
    assignRole: vi.fn(),
    getRoles: vi.fn(),
    updateSupplierConfig: vi.fn(),
    getPurchaseSettings: vi.fn(),
    getRetentionDefaults: vi.fn(),
    addRetentionDefault: vi.fn(),
    setRetentionDefaultState: vi.fn(),
  },
}));

vi.mock("../api/paymentTermService", () => ({
  paymentTermService: {
    list: vi.fn(),
  },
}));

vi.mock("../api/useSriSupplierTypes", () => ({
  useSriSupplierTypes: () => ({ options: [{ code: "01", name: "Persona Natural" }], loading: false }),
}));

vi.mock("../api/useSriPaymentMethods", () => ({
  useSriPaymentMethods: () => ({ options: [{ code: "01", name: "Sin sistema financiero" }], loading: false, error: null }),
}));

vi.mock("../api/useSriTaxSupportCodes", () => ({
  useSriTaxSupportCodes: () => ({
    options: [{ code: "01", name: "Crédito Tributario" }],
    loading: false,
    error: null,
  }),
}));

vi.mock("../api/useSriRetentionCodes", () => ({
  useSriRetentionCodes: (taxType: "IVA" | "RENTA") =>
    taxType === "IVA"
      ? {
          options: [
            {
              id: "src-725",
              taxType: "IVA",
              code: "725",
              name: "Retención en la fuente IVA 100%",
              percentage: 100,
              appliesTo: "SUPPLIER",
            },
          ],
          loading: false,
          error: null,
        }
      : {
          options: [
            {
              id: "src-303",
              taxType: "RENTA",
              code: "303",
              name: "Honorarios profesionales",
              percentage: 10,
              appliesTo: "SUPPLIER",
            },
          ],
          loading: false,
          error: null,
        },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    confirm: vi.fn(),
  },
}));

const SUPPLIER: BusinessPartnerSummaryDto = {
  id: "bp-2",
  identificationType: "04",
  identificationNumber: "0999999999001",
  legalName: "Proveedor Uno",
  tradeName: null,
  legalEntityTypeCode: 2,
  countryCode: "EC",
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  isCustomer: false,
  isSupplier: true,
  canAssignAsCustomer: true,
  canAssignAsSupplier: false,
};

function renderPage() {
  const utils = render(
    <I18nProvider>
      <MemoryRouter>
        <MasterDataSuppliersPage />
      </MemoryRouter>
    </I18nProvider>,
  );
  fireEvent.click(screen.getByRole("tab", { name: /Listado/i }));
  return utils;
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(businessPartnerFacade.searchBusinessPartnersPaged).mockResolvedValue({
    items: [SUPPLIER],
    totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
  });
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => {
  cleanup();
  useMasterDataSuppliersUiStore.setState({
    activeTab: "resumen",
    editingPartner: null,
    recentActivity: [],
  });
});

describe("MasterDataSuppliersPage — desactivar proveedor: confirmación y feedback", () => {
  it("pide confirmación antes de desactivar, explicando que no borra histórico", async () => {
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar proveedor" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(businessPartnerFacade.deactivateBusinessPartner).toHaveBeenCalledWith("bp-2");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/dejará de estar disponible/i);
    expect(String(options.message)).toMatch(/no se elimina/i);
  });

  it("si se cancela, no llama al backend", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar proveedor" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(businessPartnerFacade.deactivateBusinessPartner).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success (no message.info)", async () => {
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar proveedor" }));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Proveedor desactivado correctamente."),
    );
    expect(message.info).not.toHaveBeenCalled();
  });

  it("REGRESIÓN falso-éxito: si el backend falla, NO muestra éxito y sí muestra el error real", async () => {
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El proveedor tiene compras pendientes." } },
      },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar proveedor" }));

    await waitFor(() => {
      expect(message.error).toHaveBeenCalledWith("El proveedor tiene compras pendientes.");
    });
    expect(message.success).not.toHaveBeenCalled();
    expect(message.info).not.toHaveBeenCalled();
  });
});

describe("MasterDataSuppliersPage — activar proveedor: confirmación y feedback", () => {
  const INACTIVE_SUPPLIER: BusinessPartnerSummaryDto = { ...SUPPLIER, isActive: false };

  it("pide confirmación antes de activar y muestra success al confirmar", async () => {
    vi.mocked(businessPartnerFacade.searchBusinessPartnersPaged).mockResolvedValue({
      items: [INACTIVE_SUPPLIER],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
    });
    vi.mocked(businessPartnerFacade.activateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Activar proveedor" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(businessPartnerFacade.activateBusinessPartner).toHaveBeenCalledWith("bp-2");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/volverá a estar disponible/i);
    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Proveedor activado correctamente."),
    );
  });

  it("si falla, no muestra éxito falso", async () => {
    vi.mocked(businessPartnerFacade.searchBusinessPartnersPaged).mockResolvedValue({
      items: [INACTIVE_SUPPLIER],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
    });
    vi.mocked(businessPartnerFacade.activateBusinessPartner).mockRejectedValue({
      isAxiosError: true,
      response: { status: 500, data: {} },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Activar proveedor" }));

    await waitFor(() => expect(message.error).toHaveBeenCalled());
    expect(message.success).not.toHaveBeenCalled();
  });
});

// ZHToggle no asocia el label con el <button role="switch"> vía aria-label/htmlFor
// (son hermanos en el DOM) — se localiza el switch subiendo al contenedor .zh-toggle.
function getToggleSwitchByLabel(labelText: string): HTMLElement {
  const label = screen.getByText(labelText);
  const container = label.closest(".zh-toggle");
  if (!container) throw new Error(`No se encontró el contenedor .zh-toggle para "${labelText}"`);
  return within(container as HTMLElement).getByRole("switch");
}

// ZHField envuelve label + <select> en un <label> nativo, pero jsdom no siempre resuelve el
// nombre accesible por wrapping label — se localiza subiendo al contenedor .zh-field.
function getSelectByFieldLabel(labelText: string): HTMLSelectElement {
  const label = screen.getByText(labelText);
  const container = label.closest(".zh-field");
  if (!container) throw new Error(`No se encontró el contenedor .zh-field para "${labelText}"`);
  return within(container as HTMLElement).getByRole("combobox") as HTMLSelectElement;
}

describe("MasterDataSuppliersPage — Config SRI: toggle Obligado a llevar contabilidad", () => {
  const SUPPLIER_ROLE: BusinessPartnerRoleDto = {
    id: "role-1",
    roleType: "Supplier",
    roleLabel: "Proveedor",
    isActive: true,
    notes: null,
    assignedAt: "2026-08-01T00:00:00Z",
    revokedAt: null,
    supplierConfig: {
      defaultTaxSupportCode: "01",
      defaultPaymentMethodCode: "01",
      refundProviderTypeCode: "01",
      isRetentionExempt: false,
      isRequiredToKeepAccounting: true,
    },
    carrierConfig: null,
    customerConfig: null,
  };

  beforeEach(() => {
    vi.mocked(businessPartnerFacade.getRoles).mockResolvedValue([SUPPLIER_ROLE]);
    vi.mocked(businessPartnerFacade.getPurchaseSettings).mockResolvedValue({
      id: "pcbs-1",
      businessPartnerId: "bp-2",
      paymentTermId: null,
      hasCustomConfiguration: false,
    });
    vi.mocked(businessPartnerFacade.getRetentionDefaults).mockResolvedValue([]);
    vi.mocked(paymentTermService.list).mockResolvedValue([]);
  });

  it("precarga el toggle y los demás campos desde el SupplierRoleConfigDto existente", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "SRI" }));

    await waitFor(() => expect(businessPartnerFacade.getRoles).toHaveBeenCalledWith("bp-2", true));
    await waitFor(() =>
      expect(screen.getByText("Obligado a llevar contabilidad")).toBeTruthy(),
    );

    const toggle = getToggleSwitchByLabel("Obligado a llevar contabilidad");
    expect(toggle.getAttribute("aria-checked")).toBe("true");

    expect(getSelectByFieldLabel("Sustento tributario predeterminado").value).toBe("01");
  });

  it("ya no permite escribir el sustento tributario manualmente: es <select>, no <input>", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "SRI" }));

    await waitFor(() => getSelectByFieldLabel("Sustento tributario predeterminado"));
    expect(screen.queryByPlaceholderText("01")).toBeNull();
  });

  it("un código guardado que ya no existe en el catálogo se muestra como no vigente, sin descartarlo", async () => {
    vi.mocked(businessPartnerFacade.getRoles).mockResolvedValue([
      {
        ...SUPPLIER_ROLE,
        supplierConfig: {
          ...SUPPLIER_ROLE.supplierConfig!,
          defaultTaxSupportCode: "99",
        },
      },
    ]);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "SRI" }));

    await waitFor(() =>
      expect(getSelectByFieldLabel("Sustento tributario predeterminado").value).toBe("99"),
    );
    await waitFor(() =>
      expect(
        screen.getByText("Este código ya no está activo en el catálogo. Selecciona una opción válida."),
      ).toBeTruthy(),
    );
  });

  it("se puede desactivar el toggle y el body de guardado incluye isRequiredToKeepAccounting", async () => {
    vi.mocked(businessPartnerFacade.updateSupplierConfig).mockResolvedValue(SUPPLIER_ROLE);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "SRI" }));

    await screen.findByText("Obligado a llevar contabilidad");
    const toggle = getToggleSwitchByLabel("Obligado a llevar contabilidad");
    expect(toggle.getAttribute("aria-checked")).toBe("true");

    fireEvent.click(toggle);
    expect(toggle.getAttribute("aria-checked")).toBe("false");

    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() =>
      expect(businessPartnerFacade.updateSupplierConfig).toHaveBeenCalledWith(
        "bp-2",
        "role-1",
        expect.objectContaining({ isRequiredToKeepAccounting: false }),
      ),
    );
  });

  it("RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01: lista, agrega y desactiva retenciones predeterminadas dinámicas", async () => {
    vi.mocked(businessPartnerFacade.getRetentionDefaults).mockResolvedValue([
      {
        id: "srd-1",
        businessPartnerId: "bp-2",
        sriRetentionCodeId: "src-303",
        taxType: "RENTA",
        code: "303",
        codeName: "Honorarios profesionales",
        percentage: 10,
        isCatalogCodeActive: true,
        isActive: true,
        displayOrder: 0,
      },
    ]);
    vi.mocked(businessPartnerFacade.addRetentionDefault).mockResolvedValue({
      id: "srd-2",
      businessPartnerId: "bp-2",
      sriRetentionCodeId: "src-725",
      taxType: "IVA",
      code: "725",
      codeName: "Retención en la fuente IVA 100%",
      percentage: 100,
      isCatalogCodeActive: true,
      isActive: true,
      displayOrder: 1,
    });
    vi.mocked(businessPartnerFacade.setRetentionDefaultState).mockResolvedValue({
      id: "srd-1",
      businessPartnerId: "bp-2",
      sriRetentionCodeId: "src-303",
      taxType: "RENTA",
      code: "303",
      codeName: "Honorarios profesionales",
      percentage: 10,
      isCatalogCodeActive: true,
      isActive: false,
      displayOrder: 0,
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "SRI" }));

    await screen.findByText("Retenciones predeterminadas");
    await waitFor(() =>
      expect(screen.getByText(/RENTA — 303 — Honorarios profesionales/)).toBeTruthy(),
    );

    // No quedan los dos selects fijos legacy en el DOM.
    expect(screen.queryByText("Código ret. IVA predeterminado")).toBeNull();
    expect(screen.queryByText("Código ret. Renta predeterminado")).toBeNull();

    // Agregar una nueva retención IVA.
    fireEvent.change(getSelectByFieldLabel("Tipo de impuesto"), {
      target: { value: "IVA" },
    });
    fireEvent.change(getSelectByFieldLabel("Código de retención SRI"), {
      target: { value: "src-725" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Agregar retención" }));

    await waitFor(() =>
      expect(businessPartnerFacade.addRetentionDefault).toHaveBeenCalledWith("bp-2", {
        sriRetentionCodeId: "src-725",
      }),
    );

    // Desactivar la retención existente (fila 303 — no la recién agregada, "Agregar retención"
    // no muta el estado local con el mock hasta que llega la respuesta, así que localizamos el
    // switch dentro de la fila específica en vez de por texto global "Activa", que ya no es único).
    const row303 = screen
      .getByText(/RENTA — 303 — Honorarios profesionales/)
      .closest(".md-supplier-retention-row");
    if (!row303) throw new Error("No se encontró la fila de retención 303");
    const toggle = within(row303 as HTMLElement).getByRole("switch");
    fireEvent.click(toggle);

    await waitFor(() =>
      expect(businessPartnerFacade.setRetentionDefaultState).toHaveBeenCalledWith(
        "bp-2",
        "srd-1",
        { isActive: false, displayOrder: 0 },
      ),
    );
  });
});

describe("MasterDataSuppliersPage — sin diálogos nativos", () => {
  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "Desactivar proveedor" }));
    await waitFor(() =>
      expect(businessPartnerFacade.deactivateBusinessPartner).toHaveBeenCalled(),
    );

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
