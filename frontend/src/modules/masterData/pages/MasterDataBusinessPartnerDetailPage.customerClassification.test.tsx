// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { classificationCatalogFacade } from "../facades/classificationCatalogFacade";
import type { CustomerRoleConfigDto } from "../types/businessPartner.types";
import { renderBpDetail } from "./bpDetailTestRender";
import { ALL_PERMS, CUSTOMER_ROLE, makeBp, permsOf } from "./bpDetailTestFixtures";

/**
 * BUG-CUSTOMERS-CLASSIFICATION-01 (migrado desde el modal «Clasificación» del listado a la pestaña
 * Cliente de la ficha unificada) — el PATCH customer-config reemplaza la configuración completa
 * (null = limpiar): el formulario parte siempre de la configuración persistida y nunca borra valores
 * sin intervención del usuario. Datos 100 % ficticios; sin backend real.
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn(),
    getLocations: vi.fn(),
    getContacts: vi.fn(),
    updateCustomerConfig: vi.fn(),
    getSalesSettings: vi.fn(),
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
vi.mock("../api/paymentTermService", () => ({ paymentTermService: { list: vi.fn().mockResolvedValue([]) } }));
vi.mock("../api/useSriIdTypes", () => ({
  useSriIdTypes: () => ({ options: [], loading: false }),
  useSriIdTypesByUsage: () => ({ options: [], loading: false }),
  getSriIdTypeName: (code: string) => code,
}));
vi.mock("../api/useLegalEntityTypes", () => ({ useLegalEntityTypes: () => ({ options: [], loading: false }) }));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), confirm: vi.fn() },
}));

const EXISTING: Required<CustomerRoleConfigDto> = {
  customerCategory: "WHOLESALE",
  customerSegment: "SMB",
  salesZone: "Norte",
  creditRating: "AA",
  loyaltyTier: "GOLD",
  preferredInvoiceFormat: "PDF",
  customerClassification: "NACIONAL",
};
const ALL_NULL = {
  customerCategory: null, customerSegment: null, salesZone: null, creditRating: null,
  loyaltyTier: null, preferredInvoiceFormat: null, customerClassification: null,
};
const item = (code: string) => ({ id: `id-${code}`, code, name: `Nombre ${code}`, sortOrder: 1 });
const roleWith = (config: CustomerRoleConfigDto | null) => ({ ...CUSTOMER_ROLE, customerConfig: config });

const section = () => screen.getByRole("heading", { name: "Clasificación comercial" }).parentElement as HTMLElement;
const saveButton = () => within(section()).getByRole("button", { name: "Guardar" }) as HTMLButtonElement;
/** Orden del formulario: categoría, segmento, rating, fidelización, formato, clasificación. */
const selects = () => within(section()).getAllByRole("combobox") as HTMLSelectElement[];
const zoneInput = () => within(section()).getByPlaceholderText("Norte, Sur, Centro…") as HTMLInputElement;

async function openReady() {
  renderBpDetail("?tab=classification");
  await screen.findByRole("heading", { name: "Clasificación comercial" });
  await waitFor(() => expect(saveButton().disabled).toBe(false));
}
const loadWith = (config: CustomerRoleConfigDto | null) =>
  vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([roleWith(config)]));

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue(permsOf(ALL_PERMS) as unknown as ReturnType<typeof usePermissionsUi>);
  loadWith({ ...EXISTING });
  vi.mocked(businessPartnerFacade.getLocations).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getContacts).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getSalesSettings).mockResolvedValue({ paymentTermId: null } as never);
  vi.mocked(businessPartnerFacade.updateCustomerConfig).mockResolvedValue(undefined as never);
  vi.mocked(classificationCatalogFacade.customerCategories).mockResolvedValue([item("RETAIL"), item("WHOLESALE")]);
  vi.mocked(classificationCatalogFacade.customerSegments).mockResolvedValue([item("SMB"), item("ENTERPRISE")]);
  vi.mocked(classificationCatalogFacade.customerCreditRatings).mockResolvedValue([item("AA"), item("B")]);
  vi.mocked(classificationCatalogFacade.loyaltyTiers).mockResolvedValue([item("GOLD"), item("SILVER")]);
  vi.mocked(classificationCatalogFacade.customerInvoiceFormats).mockResolvedValue([item("PDF"), item("XML")]);
  vi.mocked(classificationCatalogFacade.customerClassifications).mockResolvedValue([item("NACIONAL"), item("EXENTO")]);
});
afterEach(() => cleanup());

describe("clasificación del cliente — configuración existente", () => {
  it("carga los valores persistidos y abrir la pestaña no escribe", async () => {
    await openReady();
    expect(selects().map((s) => s.value)).toEqual(["WHOLESALE", "SMB", "AA", "GOLD", "PDF", "NACIONAL"]);
    expect(zoneInput().value).toBe("Norte");
    expect(businessPartnerFacade.updateCustomerConfig).not.toHaveBeenCalled();
  });

  it("modificar un campo conserva los demás (reemplazo completo con lo persistido)", async () => {
    await openReady();
    fireEvent.change(selects()[1], { target: { value: "ENTERPRISE" } });
    fireEvent.click(saveButton());
    await waitFor(() => expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledTimes(1));
    expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledWith("bp-1", "role-c", {
      ...EXISTING, customerSegment: "ENTERPRISE",
    });
    await waitFor(() => expect(message.success).toHaveBeenCalledWith("Clasificación del cliente guardada correctamente."));
  });

  it("limpiar deliberadamente un campo envía null solo para ese campo", async () => {
    await openReady();
    fireEvent.change(selects()[0], { target: { value: "" } });
    fireEvent.change(zoneInput(), { target: { value: "   " } });
    fireEvent.click(saveButton());
    await waitFor(() => expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledTimes(1));
    expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledWith("bp-1", "role-c", {
      ...EXISTING, customerCategory: null, salesZone: null,
    });
  });

  it("conserva y reenvía un valor persistido que ya no está en el catálogo activo", async () => {
    loadWith({ ...EXISTING, loyaltyTier: "PLATINUM" });
    await openReady();
    expect(selects()[3].value).toBe("PLATINUM");
    expect(within(section()).getByRole("option", { name: "PLATINUM (fuera del catálogo activo)" })).toBeTruthy();
    fireEvent.change(zoneInput(), { target: { value: "Sur" } });
    fireEvent.click(saveButton());
    await waitFor(() => expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledTimes(1));
    expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledWith("bp-1", "role-c", {
      ...EXISTING, loyaltyTier: "PLATINUM", salesZone: "Sur",
    });
  });

  it("si falla el guardado, mantiene el formulario con los valores editados y muestra el error", async () => {
    vi.mocked(businessPartnerFacade.updateCustomerConfig).mockRejectedValue({
      isAxiosError: true, response: { status: 422, data: { message: { user: "Valor de catálogo no válido." } } },
    });
    await openReady();
    fireEvent.change(selects()[1], { target: { value: "ENTERPRISE" } });
    fireEvent.click(saveButton());
    expect(await within(section()).findByText("Valor de catálogo no válido.")).toBeTruthy();
    expect(selects().map((s) => s.value)).toEqual(["WHOLESALE", "ENTERPRISE", "AA", "GOLD", "PDF", "NACIONAL"]);
    expect(zoneInput().value).toBe("Norte");
    expect(message.success).not.toHaveBeenCalled();
  });
});

describe("clasificación del cliente — sin configuración y catálogos", () => {
  it("cliente sin configuración abre vacío y guardar sin cambios envía todo null", async () => {
    loadWith(null);
    await openReady();
    expect(selects().every((s) => s.value === "")).toBe(true);
    expect(zoneInput().value).toBe("");
    fireEvent.click(saveButton());
    await waitFor(() => expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledTimes(1));
    expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledWith("bp-1", "role-c", ALL_NULL);
  });

  it("bloquea el guardado mientras los catálogos cargan", async () => {
    vi.mocked(classificationCatalogFacade.customerSegments).mockReturnValue(new Promise(() => {}));
    renderBpDetail("?tab=classification");
    await screen.findByRole("heading", { name: "Clasificación comercial" });
    expect(saveButton().disabled).toBe(true);
    fireEvent.submit(saveButton().closest("form")!);
    expect(businessPartnerFacade.updateCustomerConfig).not.toHaveBeenCalled();
  });

  it("bloquea el guardado si falla la carga de un catálogo", async () => {
    vi.mocked(classificationCatalogFacade.customerCategories).mockRejectedValue(new Error("catálogo caído"));
    renderBpDetail("?tab=classification");
    await screen.findByRole("heading", { name: "Clasificación comercial" });
    await waitFor(() => expect(selects()[1].value).toBe("SMB"));
    expect(saveButton().disabled).toBe(true);
    fireEvent.submit(saveButton().closest("form")!);
    expect(businessPartnerFacade.updateCustomerConfig).not.toHaveBeenCalled();
  });
});
