// @vitest-environment jsdom
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { ensureLocaleLoaded } from "../../../../i18n/dictionaries";
import { useAuthStore } from "../../../../store/authStore";
import { useSessionStore } from "../../../../store/sessionStore";
import { useActiveBranchStore } from "../../../../store/activeBranchStore";
import { NavigationAccessContext, buildNavigationAccess } from "../../../../components/navigationAccess";
import type { MainMenuGroup } from "../../../../components/useAppLayoutNavigation";
import { CompanySettingsHubPage } from "./CompanySettingsHubPage";
import { ReadinessRequirementRow } from "../sections/ReadinessRequirementRow";

// NAV-COVERAGE-H2 — hook real de permisos (sin dobles de usePermissionsUi): cada pestaña exige el
// permiso de lectura de su API; erp.companies.view y settings.company.view no se implican.

const getProfile = vi.fn(async () => ({ id: "c", legalName: "Fixture", tradeName: null, taxIdentificationNumber: "1790016919001", taxIdentificationStatus: "Verified", isActive: true, currencyCode: "USD", timezone: "America/Guayaquil", languageCode: "es", updatedAt: "2026-11-01T12:00:00Z", isAccountingReq: false, isForeignTrade: false, withholdsRenta: false, withholdsVat: false, onboardingCompleted: true, extraLegend: null, specialTaxpayerNo: null }));
const getCompanyInvoiceDefaults = vi.fn(async () => ({ defaultDocTypeCode: null, defaultSriPaymentMethodCode: null, defaultPaymentTermId: null }));
vi.mock("../api/companyProfileService", () => ({ companyProfileService: { getProfile: () => getProfile() } }));
vi.mock("../api/orgConfigService", () => ({ orgConfigService: { getCompanyInvoiceDefaults: () => getCompanyInvoiceDefaults() } }));
vi.mock("../../../items/facades/sriLookupFacade", () => ({ sriLookupFacade: { docTypes: async () => [], paymentMethods: async () => [] } }));
vi.mock("../../../masterData/facades/paymentTermLookupFacade", () => ({ paymentTermLookupFacade: { list: async () => [] } }));
vi.mock("../../../pricing/facades/priceListLookupFacade", () => ({ priceListLookupFacade: { list: async () => [] } }));

const COMPANY_VIEW = "settings.company.view";
const COMPANIES_VIEW = "erp.companies.view";
const ALL_TABS = ["General", "Marca", "Fiscal / Tributario", "Operación", "Documentos", "Precisión operativa", "Ventas", "Preparación operativa"];
const NO_ACCESS = "No tienes acceso a esta pantalla.";

function signIn(role: string) {
  useAuthStore.setState({ user: { userId: "u", fullName: "U", username: "u", role } as never, isAuthenticated: true });
}
function loadSession(permissions: string[]) {
  useSessionStore.setState({ isLoaded: true, authorization: { roles: ["User"], permissions } });
}
function renderHub(tab?: string) {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[tab ? `/settings/company?tab=${tab}` : "/settings/company"]}>
        <CompanySettingsHubPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}
function tabLabels() {
  return screen.queryAllByRole("tab").map((tab) => tab.textContent);
}

beforeAll(async () => { await ensureLocaleLoaded("es"); });
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  useAuthStore.setState({ user: null, isAuthenticated: false });
  useSessionStore.setState({ isLoaded: false, authorization: null });
  useActiveBranchStore.setState({ branch: null });
});

describe("CompanySettingsHubPage — acceso por pestaña", () => {
  it("solo settings.company.view: únicamente Ventas, abierta por defecto", async () => {
    signIn("User");
    loadSession([COMPANY_VIEW]);
    renderHub();

    expect(tabLabels()).toEqual(["Ventas"]);
    expect(await screen.findByText("Configuración de Ventas")).toBeTruthy();
    expect(getProfile).not.toHaveBeenCalled();
  });

  it("solo settings.company.view: una pestaña de perfil por URL directa responde sin acceso", () => {
    signIn("User");
    loadSession([COMPANY_VIEW]);
    renderHub("profile");

    expect(tabLabels()).toEqual(["Ventas"]);
    expect(screen.getByRole("tab", { name: "Ventas" }).getAttribute("aria-selected")).toBe("false");
    expect(screen.getAllByText(NO_ACCESS).length).toBeGreaterThan(0);
    expect(getProfile).not.toHaveBeenCalled();
  });

  it("solo erp.companies.view: siete pestañas sin Ventas; Ventas por URL responde sin acceso", async () => {
    signIn("User");
    loadSession([COMPANIES_VIEW]);
    const view = renderHub("sales");

    expect(tabLabels()).toEqual(ALL_TABS.filter((label) => label !== "Ventas"));
    expect(screen.getAllByText(NO_ACCESS).length).toBeGreaterThan(0);
    expect(getCompanyInvoiceDefaults).not.toHaveBeenCalled();

    view.unmount();
    renderHub();
    expect(screen.getByRole("tab", { name: "General" }).getAttribute("aria-selected")).toBe("true");
    expect(await screen.findByText("Información Corporativa")).toBeTruthy();
  });

  it("ambos permisos: las ocho pestañas y navegación entre Ventas y General", async () => {
    signIn("User");
    loadSession([COMPANY_VIEW, COMPANIES_VIEW]);
    renderHub("sales");

    expect(tabLabels()).toEqual(ALL_TABS);
    expect(await screen.findByText("Configuración de Ventas")).toBeTruthy();

    fireEvent.click(screen.getByRole("tab", { name: "General" }));
    expect(await screen.findByText("Información Corporativa")).toBeTruthy();
    expect(screen.getByRole("tab", { name: "General" }).getAttribute("aria-selected")).toBe("true");
  });

  it.each([
    ["otros permisos", ["pricing.view"]],
    ["lista vacía (sin fallback permisivo)", []],
  ])("ningún permiso de lectura (%s): sin pestañas ni llamadas a API", (_label, permissions) => {
    signIn("User");
    loadSession(permissions);
    renderHub("sales");

    expect(screen.queryByRole("tablist")).toBeNull();
    expect(screen.getAllByText(NO_ACCESS).length).toBeGreaterThan(0);
    expect(getProfile).not.toHaveBeenCalled();
    expect(getCompanyInvoiceDefaults).not.toHaveBeenCalled();
  });

  it("mientras se cargan los permisos no concede pestañas; al resolverse aplica el permiso real", async () => {
    signIn("User");
    renderHub();

    expect(screen.getByText("Cargando...")).toBeTruthy();
    expect(screen.queryByRole("tablist")).toBeNull();
    expect(getProfile).not.toHaveBeenCalled();
    expect(getCompanyInvoiceDefaults).not.toHaveBeenCalled();

    act(() => loadSession([COMPANY_VIEW]));
    expect(tabLabels()).toEqual(["Ventas"]);
    expect(await screen.findByText("Configuración de Ventas")).toBeTruthy();
    expect(getProfile).not.toHaveBeenCalled();
  });

  it("Admin ve las ocho pestañas sin esperar la lista de permisos", () => {
    signIn("Admin");
    renderHub();
    expect(tabLabels()).toEqual(ALL_TABS);
  });
});

describe("ReadinessRequirementRow — destinos de pestañas de Datos de la empresa", () => {
  const MENU: MainMenuGroup[] = [
    { id: "settings", label: "Configuración", isActive: false, items: [{ id: "company", to: "/settings/company", label: "Datos de la empresa" }] },
  ];
  function renderRow(actionTarget: "CompanyProfile" | "CompanySalesSettings") {
    return render(
      <I18nProvider>
        <MemoryRouter>
          <NavigationAccessContext.Provider value={buildNavigationAccess(MENU, true)}>
            <ReadinessRequirementRow code="identity.taxId" status="Missing" actionTarget={actionTarget} />
          </NavigationAccessContext.Provider>
        </MemoryRouter>
      </I18nProvider>,
    );
  }

  it("solo erp.companies.view: enlaza General pero no Ventas", () => {
    signIn("User");
    loadSession([COMPANIES_VIEW]);
    const profile = renderRow("CompanyProfile");
    expect(within(profile.container).getByRole("link", { name: "Datos de la empresa" }).getAttribute("href"))
      .toBe("/settings/company?tab=profile");
    profile.unmount();

    const sales = renderRow("CompanySalesSettings");
    expect(within(sales.container).queryByRole("link")).toBeNull();
    expect(within(sales.container).getByText(/Pide a un administrador/)).toBeTruthy();
  });

  it("solo settings.company.view: enlaza Ventas pero no General", () => {
    signIn("User");
    loadSession([COMPANY_VIEW]);
    const sales = renderRow("CompanySalesSettings");
    expect(within(sales.container).getByRole("link").getAttribute("href")).toBe("/settings/company?tab=sales");
    sales.unmount();

    const profile = renderRow("CompanyProfile");
    expect(within(profile.container).queryByRole("link")).toBeNull();
  });

  it("con permisos aún sin cargar no ofrece enlace ni aviso", () => {
    signIn("User");
    const row = renderRow("CompanySalesSettings");
    expect(within(row.container).queryByRole("link")).toBeNull();
    expect(within(row.container).queryByText(/Pide a un administrador/)).toBeNull();
  });
});
