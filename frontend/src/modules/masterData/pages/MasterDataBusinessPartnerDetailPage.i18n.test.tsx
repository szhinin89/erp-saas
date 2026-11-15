// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { storageKey } from "../../../i18n/dictionaries";
import { message } from "../../../lib/messages";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { renderBpDetail } from "./bpDetailTestRender";
import { ALL_PERMS, CONTACT, CUSTOMER_ROLE, LOCATION, SUPPLIER_ROLE, makeBp, permsOf } from "./bpDetailTestFixtures";

/**
 * BP-CONSOLIDATION-01 Etapa 4 — i18n de la ficha unificada: textos del alcance traducidos en es y en,
 * parámetros interpolados (nunca llaves `{{x}}` ni claves crudas en pantalla) y sin textos fijos en el
 * idioma equivocado.
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn(), getLocations: vi.fn(), getContacts: vi.fn(), revokeRole: vi.fn(),
    deactivateLocation: vi.fn(), setLocationPrimary: vi.fn(), deactivateContact: vi.fn(), setContactPrimary: vi.fn(),
    getSalesSettings: vi.fn(), getPurchaseSettings: vi.fn(), getRetentionDefaults: vi.fn(), getExpenseSubcategories: vi.fn(),
  },
}));
vi.mock("../api/paymentTermService", () => ({ paymentTermService: { list: vi.fn().mockResolvedValue([]) } }));
vi.mock("../../branches/facades/geographyLookupFacade", () => ({
  geographyLookupFacade: {
    provinces: vi.fn().mockResolvedValue([]), cantons: vi.fn().mockResolvedValue([]), parishes: vi.fn().mockResolvedValue([]),
  },
}));
const NO_OPTIONS = { options: [], loading: false, error: null };
vi.mock("../api/useSriIdTypes", () => ({
  useSriIdTypes: () => ({ options: [], loading: false }),
  useSriIdTypesByUsage: () => ({ options: [], loading: false }),
  getSriIdTypeName: (code: string) => code,
}));
vi.mock("../api/useLegalEntityTypes", () => ({ useLegalEntityTypes: () => ({ options: [], loading: false }) }));
vi.mock("../api/useSriSupplierTypes", () => ({ useSriSupplierTypes: () => NO_OPTIONS }));
vi.mock("../api/useSriPaymentMethods", () => ({ useSriPaymentMethods: () => NO_OPTIONS }));
vi.mock("../api/useSriTaxSupportCodes", () => ({ useSriTaxSupportCodes: () => NO_OPTIONS }));
vi.mock("../api/useSriRetentionCodes", () => ({ useSriRetentionCodes: () => NO_OPTIONS }));
vi.mock("../api/useClassificationCatalogs", () => ({
  useCustomerCategories: () => NO_OPTIONS, useCustomerSegments: () => NO_OPTIONS, useCustomerCreditRatings: () => NO_OPTIONS,
  useLoyaltyTiers: () => NO_OPTIONS, useCustomerInvoiceFormats: () => NO_OPTIONS, useCustomerClassifications: () => NO_OPTIONS,
}));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

const SUPPLIER_TABS = ["summary", "general", "sri", "purchases", "retentions", "expenseSubcategories", "locations", "contacts"];
const CUSTOMER_TABS = ["summary", "general", "classification", "sales", "locations", "contacts"];
const RETENTION = {
  id: "srd-1", businessPartnerId: "bp-1", sriRetentionCodeId: "src-1", taxType: "RENTA", code: "303", codeName: "Honorarios",
  percentage: 10, isCatalogCodeActive: true, isActive: true, displayOrder: 0,
};

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.removeItem(storageKey);
  vi.mocked(usePermissionsUi).mockReturnValue(permsOf(ALL_PERMS) as unknown as ReturnType<typeof usePermissionsUi>);
  vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([CUSTOMER_ROLE, SUPPLIER_ROLE]));
  vi.mocked(businessPartnerFacade.getLocations).mockResolvedValue([LOCATION, { ...LOCATION, id: "loc-2", name: "Bodega Sur", isPrimary: false }]);
  vi.mocked(businessPartnerFacade.getContacts).mockResolvedValue([CONTACT, { ...CONTACT, id: "con-2", fullName: "Luis Mora", isPrimary: false }]);
  vi.mocked(businessPartnerFacade.getSalesSettings).mockResolvedValue({ paymentTermId: null } as never);
  vi.mocked(businessPartnerFacade.getPurchaseSettings).mockResolvedValue({ paymentTermId: null } as never);
  vi.mocked(businessPartnerFacade.getRetentionDefaults).mockResolvedValue([RETENTION] as never);
  vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue({ mode: "Unconfigured", items: [], availableSubcategoryIds: [], catalog: null } as never);
  vi.mocked(message.confirm).mockResolvedValue(true);
});
afterEach(() => { cleanup(); localStorage.removeItem(storageKey); });

/** Texto visible de todos los paneles (incluidos los ocultos ya visitados) y atributos accesibles. */
const visibleText = () => document.body.textContent ?? "";
const a11yAttrs = () =>
  [...document.querySelectorAll("[aria-label],[title],[placeholder]")]
    .map((e) => [e.getAttribute("aria-label"), e.getAttribute("title"), e.getAttribute("placeholder")].join(" "))
    .join(" ");
const expectNoRawI18n = () => {
  expect(visibleText()).not.toMatch(/\{\{\s*\w+\s*\}\}/);
  expect(visibleText()).not.toMatch(/masterdata\.bp\.detail\./);
  expect(a11yAttrs()).not.toMatch(/\{\{\s*\w+\s*\}\}|masterdata\.bp\.detail\./);
};

async function visitAllTabs(tabs: string[]) {
  await screen.findByRole("tablist");
  for (const tab of tabs) {
    fireEvent.click(document.getElementById(`bp-tab-${tab}`)!);
    await waitFor(() => expect(document.getElementById(`bp-panel-${tab}`)!.hasAttribute("hidden")).toBe(false));
    await new Promise((r) => setTimeout(r, 30));
  }
}

describe("ficha unificada — i18n", () => {
  it("español (proveedor): ninguna pestaña muestra claves crudas ni marcadores sin resolver", async () => {
    renderBpDetail("?tab=summary", { context: "supplier" });
    await visitAllTabs(SUPPLIER_TABS);
    expectNoRawI18n();
    expect(screen.getByRole("tab", { name: /^Ubicaciones/ })).toBeTruthy();
  });

  it("español (cliente): ninguna pestaña muestra claves crudas ni marcadores sin resolver", async () => {
    renderBpDetail("?tab=summary", { context: "customer" });
    await visitAllTabs(CUSTOMER_TABS);
    expectNoRawI18n();
  });

  it("inglés (proveedor): los textos de la ficha se muestran en inglés en todas las pestañas", async () => {
    localStorage.setItem(storageKey, "en");
    renderBpDetail("?tab=summary", { context: "supplier" });
    await visitAllTabs(SUPPLIER_TABS);
    expectNoRawI18n();
    for (const name of [/^Overview/, /^General data/, /^SRI configuration/, /^Purchases/, /^Withholdings/, /^Expense subcategories/, /^Locations/, /^Contacts/])
      expect(screen.getByRole("tab", { name })).toBeTruthy();
    const text = visibleText();
    for (const english of ["New location", "New contact", "Default withholdings", "Back to suppliers"])
      expect(text, english).toContain(english);
    for (const spanish of ["Nueva ubicación", "Nuevo contacto", "Retenciones predeterminadas", "Volver", "Datos generales", "Sustento tributario"])
      expect(text, spanish).not.toContain(spanish);
  });

  it("inglés (cliente): los textos de la ficha se muestran en inglés en todas las pestañas", async () => {
    localStorage.setItem(storageKey, "en");
    renderBpDetail("?tab=summary", { context: "customer" });
    await visitAllTabs(CUSTOMER_TABS);
    expectNoRawI18n();
    for (const name of [/^Overview/, /^General data/, /^Classification/, /^Sales terms/, /^Locations/, /^Contacts/])
      expect(screen.getByRole("tab", { name })).toBeTruthy();
    const text = visibleText();
    expect(text).toContain("Commercial classification");
    expect(text).toContain("Back to customers");
    for (const spanish of ["Clasificación comercial", "Volver", "Datos generales"])
      expect(text, spanish).not.toContain(spanish);
  });

  it("los parámetros se interpolan en confirmaciones y avisos (ubicaciones y contactos)", async () => {
    vi.mocked(businessPartnerFacade.deactivateLocation).mockResolvedValue(true);
    vi.mocked(businessPartnerFacade.setLocationPrimary).mockResolvedValue(true);
    vi.mocked(businessPartnerFacade.deactivateContact).mockResolvedValue(true);
    vi.mocked(businessPartnerFacade.setContactPrimary).mockResolvedValue(true);
    vi.mocked(businessPartnerFacade.revokeRole).mockResolvedValue(true);
    renderBpDetail("?tab=locations");
    await screen.findByText("Bodega Sur");

    const cards = () => document.querySelectorAll(".md-location-card");
    const bodega = [...cards()].find((c) => c.textContent?.includes("Bodega Sur")) as HTMLElement;
    fireEvent.click([...bodega.querySelectorAll("button")].find((b) => b.textContent === "Principal")!);
    await waitFor(() => expect(message.success).toHaveBeenCalledWith('"Bodega Sur" marcada como ubicación principal.'));
    fireEvent.click([...bodega.querySelectorAll("button")].find((b) => b.textContent === "Desactivar")!);
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    let options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(options.title).toBe('Desactivar "Bodega Sur"');
    expect(String(options.message)).toContain('"Bodega Sur" dejará de estar disponible');

    fireEvent.click(screen.getByRole("tab", { name: /^Contactos/ }));
    const luis = (await screen.findByText("Luis Mora")).closest(".md-contact-card") as HTMLElement;
    fireEvent.click([...luis.querySelectorAll("button")].find((b) => b.textContent === "Principal")!);
    await waitFor(() => expect(message.success).toHaveBeenCalledWith('"Luis Mora" marcado como contacto principal.'));
    fireEvent.click([...luis.querySelectorAll("button")].find((b) => b.textContent === "Desactivar")!);
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(2));
    options = vi.mocked(message.confirm).mock.calls[1][0];
    expect(options.title).toBe('Desactivar "Luis Mora"');
    expect(String(options.message)).toContain('"Luis Mora" dejará de estar disponible');
  });

  it("inglés: las confirmaciones y avisos con parámetros también se interpolan", async () => {
    localStorage.setItem(storageKey, "en");
    vi.mocked(businessPartnerFacade.setLocationPrimary).mockResolvedValue(true);
    renderBpDetail("?tab=locations");
    const bodega = (await screen.findByText("Bodega Sur")).closest(".md-location-card") as HTMLElement;
    fireEvent.click([...bodega.querySelectorAll("button")].find((b) => b.textContent === "Set as primary")!);
    await waitFor(() => expect(message.success).toHaveBeenCalledWith('"Bodega Sur" set as the primary location.'));
  });
});
