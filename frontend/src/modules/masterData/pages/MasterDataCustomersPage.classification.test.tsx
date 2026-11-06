// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, within, waitFor, fireEvent, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataCustomersPage } from "./MasterDataCustomersPage";
import { useMasterDataCustomersUiStore } from "../store/masterDataPartnerUiStore";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { classificationCatalogFacade } from "../facades/classificationCatalogFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import type {
  BusinessPartnerRoleDto,
  BusinessPartnerSummaryDto,
  CustomerRoleConfigDto,
} from "../types/businessPartner.types";

/**
 * BUG-CUSTOMERS-CLASSIFICATION-01 — el PATCH customer-config reemplaza la configuración completa
 * (null = limpiar). Antes el modal "Clasificación" abría con todos los campos vacíos y "Guardar"
 * borraba la clasificación persistida. Datos 100 % ficticios; sin backend real.
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    searchBusinessPartnersPaged: vi.fn(),
    getRoles: vi.fn(),
    updateCustomerConfig: vi.fn(),
  },
}));

vi.mock("../facades/classificationCatalogFacade", () => ({
  classificationCatalogFacade: {
    customerCategories: vi.fn(),
    customerSegments: vi.fn(),
    customerCreditRatings: vi.fn(),
    loyaltyTiers: vi.fn(),
    customerInvoiceFormats: vi.fn(),
    customerClassifications: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), confirm: vi.fn() },
}));

const CUSTOMER: BusinessPartnerSummaryDto = {
  id: "bp-ficticio-1",
  identificationType: "05",
  identificationNumber: "0999999999",
  legalName: "Cliente Ficticio Uno",
  tradeName: null,
  legalEntityTypeCode: 1,
  countryCode: "EC",
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  isCustomer: true,
  isSupplier: false,
  canAssignAsCustomer: false,
  canAssignAsSupplier: true,
};

const EXISTING: Required<CustomerRoleConfigDto> = {
  customerCategory: "WHOLESALE",
  customerSegment: "SMB",
  salesZone: "Norte",
  creditRating: "AA",
  loyaltyTier: "GOLD",
  preferredInvoiceFormat: "PDF",
  customerClassification: "NACIONAL",
};

const item = (code: string) => ({ id: `id-${code}`, code, name: `Nombre ${code}`, sortOrder: 1 });

function role(config: CustomerRoleConfigDto | null, roleType = "Customer"): BusinessPartnerRoleDto {
  return {
    id: "role-ficticio-1",
    roleType,
    roleLabel: roleType,
    isActive: true,
    notes: null,
    assignedAt: "2026-08-01T00:00:00Z",
    revokedAt: null,
    supplierConfig: null,
    carrierConfig: null,
    customerConfig: config,
  };
}

function renderPage() {
  render(
    <I18nProvider>
      <MemoryRouter>
        <MasterDataCustomersPage />
      </MemoryRouter>
    </I18nProvider>,
  );
  fireEvent.click(screen.getByRole("tab", { name: /Listado/i }));
}

const dialog = () => screen.getByRole("dialog");
const saveButton = () => within(dialog()).getByRole("button", { name: "Guardar" }) as HTMLButtonElement;
/** Orden del formulario: categoría, segmento, rating, fidelización, formato, clasificación. */
const selects = () => within(dialog()).getAllByRole("combobox") as HTMLSelectElement[];
const zoneInput = () => within(dialog()).getByPlaceholderText("Norte, Sur, Centro…") as HTMLInputElement;

async function openClassification() {
  fireEvent.click(await screen.findByRole("button", { name: /^Más acciones:/ }));
  fireEvent.click(screen.getByRole("menuitem", { name: "Clasificación" }));
}

async function openReady() {
  await openClassification();
  await waitFor(() => expect(saveButton().disabled).toBe(false));
}

beforeEach(() => {
  vi.clearAllMocks();
  useMasterDataCustomersUiStore.setState({ activeTab: "listado", editingPartner: null, recentActivity: [] });
  vi.mocked(usePermissionsUi).mockReturnValue({ canShow: () => true, has: () => true, isAdminRole: true });
  vi.mocked(businessPartnerFacade.searchBusinessPartnersPaged).mockResolvedValue({
    items: [CUSTOMER], totalCount: 1, pageNumber: 1, pageSize: 50,
  });
  vi.mocked(businessPartnerFacade.getRoles).mockResolvedValue([role({ ...EXISTING })]);
  vi.mocked(businessPartnerFacade.updateCustomerConfig).mockResolvedValue(role({ ...EXISTING }));
  vi.mocked(classificationCatalogFacade.customerCategories).mockResolvedValue([item("RETAIL"), item("WHOLESALE")]);
  vi.mocked(classificationCatalogFacade.customerSegments).mockResolvedValue([item("SMB"), item("ENTERPRISE")]);
  vi.mocked(classificationCatalogFacade.customerCreditRatings).mockResolvedValue([item("AA"), item("B")]);
  vi.mocked(classificationCatalogFacade.loyaltyTiers).mockResolvedValue([item("GOLD"), item("SILVER")]);
  vi.mocked(classificationCatalogFacade.customerInvoiceFormats).mockResolvedValue([item("PDF"), item("XML")]);
  vi.mocked(classificationCatalogFacade.customerClassifications).mockResolvedValue([item("NACIONAL"), item("EXENTO")]);
});

afterEach(() => {
  cleanup();
  useMasterDataCustomersUiStore.setState({ activeTab: "resumen", editingPartner: null, recentActivity: [] });
});

describe("BUG-CUSTOMERS-CLASSIFICATION-01 — cliente con configuración existente", () => {
  it("carga los valores persistidos y abrir la ventana no escribe", async () => {
    renderPage();
    await openReady();
    expect(businessPartnerFacade.getRoles).toHaveBeenCalledWith(CUSTOMER.id, true);
    expect(selects().map((s) => s.value)).toEqual(["WHOLESALE", "SMB", "AA", "GOLD", "PDF", "NACIONAL"]);
    expect(zoneInput().value).toBe("Norte");
    expect(businessPartnerFacade.updateCustomerConfig).not.toHaveBeenCalled();
  });

  it("guardar sin modificar conserva todos los valores", async () => {
    renderPage();
    await openReady();
    fireEvent.click(saveButton());
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledWith(CUSTOMER.id, "role-ficticio-1", EXISTING);
  });

  it("modificar un campo conserva los demás", async () => {
    renderPage();
    await openReady();
    fireEvent.change(selects()[1], { target: { value: "ENTERPRISE" } });
    fireEvent.click(saveButton());
    await waitFor(() => expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledTimes(1));
    expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledWith(CUSTOMER.id, "role-ficticio-1", {
      ...EXISTING, customerSegment: "ENTERPRISE",
    });
  });

  it("limpiar deliberadamente un campo envía null solo para ese campo (contrato de reemplazo)", async () => {
    renderPage();
    await openReady();
    fireEvent.change(selects()[0], { target: { value: "" } });
    fireEvent.change(zoneInput(), { target: { value: "   " } });
    fireEvent.click(saveButton());
    await waitFor(() => expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledTimes(1));
    expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledWith(CUSTOMER.id, "role-ficticio-1", {
      ...EXISTING, customerCategory: null, salesZone: null,
    });
  });

  it("conserva y reenvía un valor persistido que ya no está en el catálogo activo", async () => {
    vi.mocked(businessPartnerFacade.getRoles).mockResolvedValue([role({ ...EXISTING, loyaltyTier: "PLATINUM" })]);
    renderPage();
    await openReady();
    expect(selects()[3].value).toBe("PLATINUM");
    expect(within(dialog()).getByRole("option", { name: "PLATINUM (fuera del catálogo activo)" })).toBeTruthy();
    fireEvent.click(saveButton());
    await waitFor(() => expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledTimes(1));
    expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledWith(CUSTOMER.id, "role-ficticio-1", {
      ...EXISTING, loyaltyTier: "PLATINUM",
    });
  });

  it("si falla el guardado, mantiene la ventana con los valores y muestra el error", async () => {
    vi.mocked(businessPartnerFacade.updateCustomerConfig).mockRejectedValue({
      isAxiosError: true, response: { status: 422, data: { message: { user: "Valor de catálogo no válido." } } },
    });
    renderPage();
    await openReady();
    fireEvent.change(selects()[1], { target: { value: "ENTERPRISE" } });
    fireEvent.click(saveButton());
    expect(await within(dialog()).findByText("Valor de catálogo no válido.")).toBeTruthy();
    expect(selects().map((s) => s.value)).toEqual(["WHOLESALE", "ENTERPRISE", "AA", "GOLD", "PDF", "NACIONAL"]);
    expect(zoneInput().value).toBe("Norte");
    expect(message.success).not.toHaveBeenCalled();
  });
});

describe("BUG-CUSTOMERS-CLASSIFICATION-01 — sin configuración y errores de lectura", () => {
  it("cliente sin configuración abre vacío y guardar sin cambios envía todo null", async () => {
    vi.mocked(businessPartnerFacade.getRoles).mockResolvedValue([role(null)]);
    renderPage();
    await openReady();
    expect(selects().every((s) => s.value === "")).toBe(true);
    expect(zoneInput().value).toBe("");
    fireEvent.click(saveButton());
    await waitFor(() => expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledTimes(1));
    expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledWith(CUSTOMER.id, "role-ficticio-1", {
      customerCategory: null, customerSegment: null, salesZone: null, creditRating: null,
      loyaltyTier: null, preferredInvoiceFormat: null, customerClassification: null,
    });
  });

  it("un fallo de lectura no abre una ventana vacía ni escribe, y muestra el error", async () => {
    vi.mocked(businessPartnerFacade.getRoles).mockRejectedValue({ isAxiosError: true, response: { status: 500, data: {} } });
    renderPage();
    await openClassification();
    expect(await screen.findByText(/No se pudo cargar la clasificación del cliente/)).toBeTruthy();
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(businessPartnerFacade.updateCustomerConfig).not.toHaveBeenCalled();
  });

  it("sin rol de cliente activo informa y no abre la ventana", async () => {
    vi.mocked(businessPartnerFacade.getRoles).mockResolvedValue([role(null, "Supplier")]);
    renderPage();
    await openClassification();
    expect(await screen.findByText(/No se encontró un rol de cliente activo/)).toBeTruthy();
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("bloquea el guardado mientras los catálogos cargan", async () => {
    vi.mocked(classificationCatalogFacade.customerSegments).mockReturnValue(new Promise(() => {}));
    renderPage();
    await openClassification();
    await screen.findByRole("dialog");
    expect(saveButton().disabled).toBe(true);
    fireEvent.submit(saveButton().closest("form")!);
    expect(businessPartnerFacade.updateCustomerConfig).not.toHaveBeenCalled();
  });

  it("bloquea el guardado si falla la carga de un catálogo", async () => {
    vi.mocked(classificationCatalogFacade.customerCategories).mockRejectedValue(new Error("catálogo caído"));
    renderPage();
    await openClassification();
    await screen.findByRole("dialog");
    await waitFor(() => expect(selects()[1].value).toBe("SMB"));
    expect(saveButton().disabled).toBe(true);
    fireEvent.submit(saveButton().closest("form")!);
    expect(businessPartnerFacade.updateCustomerConfig).not.toHaveBeenCalled();
  });
});
