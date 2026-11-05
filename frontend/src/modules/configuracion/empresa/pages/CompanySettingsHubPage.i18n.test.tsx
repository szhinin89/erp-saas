// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider, useI18n } from "../../../../i18n/i18n";
import { ensureLocaleLoaded, storageKey } from "../../../../i18n/dictionaries";
import es from "../../../../i18n/locales/es.json";
import en from "../../../../i18n/locales/en.json";
import { useActiveBranchStore } from "../../../../store/activeBranchStore";
import { useSessionStore } from "../../../../store/sessionStore";
import { CompanySettingsHubPage } from "./CompanySettingsHubPage";

vi.mock("../../../../access/usePermissionsUi", () => ({ usePermissionsUi: () => ({ canShow: () => true }) }));
vi.mock("../api/companyProfileService", () => ({ companyProfileService: {
  getProfile: async () => ({ id: "test", legalName: "Fixture", tradeName: null, taxIdentificationNumber: "1790016919001", taxIdentificationStatus: "Verified", isActive: true, currencyCode: "USD", timezone: "America/Guayaquil", languageCode: "es", updatedAt: "2026-11-01T12:00:00Z", isAccountingReq: false, isForeignTrade: false, withholdsRenta: false, withholdsVat: false, onboardingCompleted: true, extraLegend: "Fixture note", specialTaxpayerNo: "ABC" }),
  getBranding: async () => ({ primaryColor: "#123456", secondaryColor: null, slogan: null, documentFooterText: null }),
  getFiscalPolicy: async () => ({ consumerFinalMaxAmount: 50, consumerFinalMaxAmountSource: "TaxRegimeDefault" }),
  getOperationalReadiness: async () => ({ branchId: "test-branch", overallStatus: "Ready", canSell: true, canIssueElectronicInvoices: true, canUseInventory: true, canUseCashRegister: true, cashRegisters: [], sections: [] }),
} }));
vi.mock("../api/orgConfigService", () => ({ orgConfigService: { getCompanyInvoiceDefaults: async () => ({ defaultDocTypeCode: null, defaultSriPaymentMethodCode: null, defaultPaymentTermId: null }) } }));
vi.mock("../../../items/facades/sriLookupFacade", () => ({ sriLookupFacade: { taxRegimes: async () => [], docTypes: async () => [], paymentMethods: async () => [] } }));
vi.mock("../../../masterData/facades/paymentTermLookupFacade", () => ({ paymentTermLookupFacade: { list: async () => [] } }));
vi.mock("../../../pricing/facades/priceListLookupFacade", () => ({ priceListLookupFacade: { list: async () => [] } }));
vi.mock("../../../../lib/config/precisionPolicy.config", async importOriginal => {
  const original = await importOriginal<typeof import("../../../../lib/config/precisionPolicy.config")>();
  const { TEST_PRECISION_POLICY, TEST_PRECISION_METADATA } = await import("../../../../test/precisionPolicyFixture");
  return { ...original, loadPrecisionPolicy: async () => TEST_PRECISION_POLICY, loadPrecisionPolicyMetadata: async () => TEST_PRECISION_METADATA };
});

function Controls() {
  const { setLocale } = useI18n();
  return <><button onClick={() => setLocale("en")}>English</button><button onClick={() => setLocale("es")}>Español</button></>;
}
// El hub exige sesión resuelta (NAV-COVERAGE-H2); estas pruebas cubren textos con acceso total.
beforeEach(() => {
  useActiveBranchStore.setState({ branch: { id: "test-branch", name: "Fixture", isMainBranch: true } });
  useSessionStore.setState({ isLoaded: true, authorization: { roles: ["User"], permissions: ["*"] } });
});
afterEach(() => { cleanup(); localStorage.clear(); useActiveBranchStore.setState({ branch: null }); useSessionStore.setState({ isLoaded: false, authorization: null }); });

const tabs = [
  ["profile", "settings.company.profile.corporate"],
  ["branding", "settings.company.branding.identity.title"],
  ["fiscal", "settings.company.fiscal.consumerRules"],
  ["operation", "settings.company.operation.title"],
  ["documents", "settings.company.documents.title"],
  ["decimals", "settings.company.precision.title"],
  ["sales", "settings.company.sales.title"],
  ["readiness", "operationalReadiness.overallStatus.ready"],
] as const;

for (const [tab, key] of tabs) {
  it.each(["es", "en"] as const)(`${tab}: textos y cambio de idioma desde %s`, async locale => {
    await ensureLocaleLoaded(locale);
    await ensureLocaleLoaded("en");
    localStorage.setItem(storageKey, locale);
    const view = render(<I18nProvider><MemoryRouter initialEntries={[`/settings/company?tab=${tab}`]}><Controls /><CompanySettingsHubPage /></MemoryRouter></I18nProvider>);
    const dictionary = locale === "en" ? en : es;
    await screen.findAllByText(dictionary[key]);
    if (tab === "sales") fireEvent.click(screen.getByRole("button", { name: new RegExp(dictionary["common.edit"]) }));
    const other = locale === "en" ? "es" : "en";
    fireEvent.click(screen.getByRole("button", { name: other === "en" ? "English" : "Español" }));
    await waitFor(() => expect(view.container.textContent).toContain((other === "en" ? en : es)[key]));
    fireEvent.click(screen.getByRole("button", { name: "English" }));
    await waitFor(() => expect(view.container.textContent).toContain(en[key]));
    // Datos del usuario/catálogos no se traducen. Estos son únicamente literales de la UI corregidos.
    for (const literal of ["Guardar Configuración", "Descartar Cambios", "Información Corporativa", "Reglas Consumidor Final", "Precisión operativa", "Estándar comercial", "Default por régimen"])
      expect(document.body.textContent).not.toContain(literal);
    expect(document.body.textContent).not.toMatch(/settings\.company\.[A-Za-z]/);
  });
}

it.each([
  ["branding", "primaryColor", "wrong", "settings.company.validation.hex"],
  ["fiscal", "specialTaxpayerNo", "$", "settings.company.validation.specialTaxpayer"],
  ["documents", "extraLegend", "x".repeat(501), "settings.company.validation.max500"],
] as const)("%s: errores de validación cambian de idioma", async (tab, field, value, key) => {
  await ensureLocaleLoaded("en");
  const view = render(<I18nProvider><MemoryRouter initialEntries={[`/settings/company?tab=${tab}`]}><Controls /><CompanySettingsHubPage /></MemoryRouter></I18nProvider>);
  await waitFor(() => expect(view.container.querySelector(`[name="${field}"]`)).toBeTruthy());
  const input = view.container.querySelector(tab === "branding" ? `input[type="text"][name="${field}"]` : `[name="${field}"]`)!;
  if (tab === "branding" || tab === "documents")
    await waitFor(() => expect((input as HTMLInputElement).value).toBe(tab === "branding" ? "#123456" : "Fixture note"));
  if (tab === "fiscal") await waitFor(() => expect((input as HTMLInputElement).value).toBe("ABC"));
  fireEvent.change(input, { target: { value } });
  fireEvent.submit(input.closest("form")!);
  await screen.findByText(es[key]);
  fireEvent.click(screen.getByRole("button", { name: "English" }));
  await screen.findByText(en[key]);
});

it("precisión: el error de rango conserva el límite del backend y cambia de idioma", async () => {
  await ensureLocaleLoaded("en");
  const view = render(<I18nProvider><MemoryRouter initialEntries={["/settings/company?tab=decimals"]}><Controls /><CompanySettingsHubPage /></MemoryRouter></I18nProvider>);
  fireEvent.click(await screen.findByRole("radio", { name: /Personalizado/ }));
  const input = view.container.querySelector('[name="salesUnitPriceDecimals"]')!;
  fireEvent.change(input, { target: { value: "99" } });
  fireEvent.submit(input.closest("form")!);
  await screen.findByText("El valor máximo es 6");
  fireEvent.click(screen.getByRole("button", { name: "English" }));
  await screen.findByText("The maximum value is 6");
});

it("marca: el error de formato de imagen cambia de idioma", async () => {
  await ensureLocaleLoaded("en");
  const view = render(<I18nProvider><MemoryRouter initialEntries={["/settings/company?tab=branding"]}><Controls /><CompanySettingsHubPage /></MemoryRouter></I18nProvider>);
  await screen.findByText(es["settings.company.branding.identity.title"]);
  fireEvent.change(view.container.querySelector('input[type="file"]')!, { target: { files: [new File(["x"], "test.txt", { type: "text/plain" })] } });
  await screen.findByText(es["settings.company.logo.formatError"]);
  fireEvent.click(screen.getByRole("button", { name: "English" }));
  await screen.findByText(en["settings.company.logo.formatError"]);
});
