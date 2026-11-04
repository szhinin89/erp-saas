// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider, useI18n } from "../../../../i18n/i18n";
import { ensureLocaleLoaded, storageKey } from "../../../../i18n/dictionaries";
import type { CompanyProfile } from "../../../../types/companyProfile";
import { companyProfileService } from "../api/companyProfileService";
import { CompanyProfileSettingsSection } from "./CompanyProfileSettingsSection";

vi.mock("../../../../access/usePermissionsUi", () => ({ usePermissionsUi: () => ({ canShow: () => true }) }));
vi.mock("../api/companyProfileService", () => ({ companyProfileService: { getProfile: vi.fn(), updateProfile: vi.fn() } }));

let stored: CompanyProfile;
beforeEach(() => {
  vi.clearAllMocks();
  stored = {
    id: "company-test", legalName: "Empresa", tradeName: "Marca", taxIdentificationNumber: "1790016919001",
    taxIdentificationStatus: "Verified", isTemporaryTaxIdentification: false,
    corporateEmail: "old@example.invalid", phone: "+59322345678", website: "https://old.example.invalid",
    countryCode: "EC", currencyCode: "USD", timezone: "America/Guayaquil",
    legalRepName: "Representante", legalRepPosition: "Gerente", legalRepIdNumber: "1710034065",
    legalRepEmail: "rep@example.invalid", legalRepPhone: "+593991234567",
    taxRegimeCode: null, isAccountingReq: false, specialTaxpayerNo: null, isForeignTrade: false,
    withholdsRenta: false, withholdsVat: false, retentionAgentResolutionNo: null,
    retentionAgentEffectiveFrom: null, largeTaxpayerResolutionNo: null, largeTaxpayerEffectiveFrom: null,
    languageCode: "es", operationalStatus: "Active", onboardingCompleted: true, extraLegend: null,
    logo: null, alternateLogo: null, isActive: true, createdAt: "2026-11-01T12:00:00Z", updatedAt: "2026-11-01T12:00:00Z",
    createdBy: null, updatedBy: null,
  };
  vi.mocked(companyProfileService.getProfile).mockImplementation(async () => ({ ...stored }));
  vi.mocked(companyProfileService.updateProfile).mockImplementation(async payload => {
    stored = { ...stored, ...payload } as CompanyProfile;
    return { ...stored };
  });
});
afterEach(() => { cleanup(); localStorage.clear(); });
const mount = () => render(<I18nProvider><MemoryRouter><CompanyProfileSettingsSection /></MemoryRouter></I18nProvider>);

function LanguageControls() {
  const { setLocale } = useI18n();
  return <><button onClick={() => setLocale("en")}>English</button><button onClick={() => setLocale("es")}>Español</button></>;
}

it.each([
  ["es", "Guardar Configuración", "Información Corporativa", "Correo electrónico inválido"],
  ["en", "Save settings", "Corporate information", "Invalid email address"],
] as const)("perfil completo y validación en %s", async (locale, save, corporate, invalid) => {
  await ensureLocaleLoaded(locale);
  localStorage.setItem(storageKey, locale);
  const view = mount();
  const email = await screen.findByDisplayValue("old@example.invalid");
  expect(screen.getByRole("button", { name: new RegExp(save) })).toBeTruthy();
  expect(screen.getByText(corporate)).toBeTruthy();
  if (locale === "en") {
    for (const label of ["Identificación", "Representante Legal", "Guardar Configuración", "Descartar Cambios", "Dólar Estadounidense", "Verificado", "Última actualización"])
      expect(view.container.textContent).not.toContain(label);
    expect(screen.getByPlaceholderText("Full name")).toBeTruthy();
    expect(screen.getByRole("combobox", { name: "Base currency" })).toBeTruthy();
  }
  fireEvent.change(email, { target: { value: "invalid" } });
  fireEvent.submit(view.container.querySelector("form")!);
  await screen.findByText(invalid);
});

it("cambiar idioma traduce campos, opciones, acciones y errores sin perder edición", async () => {
  const view = render(<I18nProvider><MemoryRouter><LanguageControls /><CompanyProfileSettingsSection /></MemoryRouter></I18nProvider>);
  const email = await screen.findByDisplayValue("old@example.invalid");
  fireEvent.change(email, { target: { value: "invalid" } });
  fireEvent.submit(view.container.querySelector("form")!);
  await screen.findByText("Correo electrónico inválido");
  fireEvent.click(screen.getByRole("button", { name: "English" }));
  await screen.findByText("Invalid email address");
  expect(screen.getByRole("button", { name: /Save settings/ })).toBeTruthy();
  expect(screen.getByDisplayValue("invalid")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Español" }));
  await screen.findByText("Correo electrónico inválido");
  expect(screen.getByRole("button", { name: /Guardar Configuración/ })).toBeTruthy();
});

// Wiring UI; la persistencia efectiva se verifica por HTTP/PostgreSQL en InternalCapabilitiesHttpTests.
it("envía todos los campos editables, recarga y conserva el perfil al reabrir", async () => {
  const view = mount();
  await screen.findByDisplayValue("old@example.invalid");
  const values = { corporateEmail: "new@example.invalid", website: "https://new.example.invalid", legalRepName: "Ana Pérez", legalRepPosition: "Directora", legalRepIdNumber: "1710034065", legalRepEmail: "ana@example.invalid", currencyCode: "EUR", timezone: "America/Lima" };
  for (const [name, value] of Object.entries(values)) {
    const field = view.container.querySelector(`[name="${name}"]`)!;
    expect(field).toBeTruthy();
    fireEvent.change(field, { target: { value } });
  }
  fireEvent.click(screen.getByRole("button", { name: /Guardar Configuración/ }));
  await waitFor(() => expect(companyProfileService.updateProfile).toHaveBeenCalledWith(expect.objectContaining(values)));
  await waitFor(() => expect(companyProfileService.getProfile).toHaveBeenCalledTimes(2));
  view.unmount();
  mount();
  await screen.findByDisplayValue("new@example.invalid");
  for (const value of ["https://new.example.invalid", "Ana Pérez", "Directora", "ana@example.invalid"]) expect(screen.getByDisplayValue(value)).toBeTruthy();
});

it("un correo inválido bloquea el envío sin cambiar lo guardado", async () => {
  const view = mount();
  const email = await screen.findByDisplayValue("old@example.invalid");
  fireEvent.change(email, { target: { value: "invalido" } });
  fireEvent.submit(view.container.querySelector("form")!);
  await screen.findByText("Correo electrónico inválido");
  expect(companyProfileService.updateProfile).not.toHaveBeenCalled();
  expect(stored.corporateEmail).toBe("old@example.invalid");
});
