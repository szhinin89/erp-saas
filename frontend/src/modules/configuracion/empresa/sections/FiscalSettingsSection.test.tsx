// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, waitFor, fireEvent } from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { ensureLocaleLoaded, storageKey } from "../../../../i18n/dictionaries";
import { HELP_KEYS, resolveHelp } from "../../../../help";
import es from "../../../../i18n/locales/es.json";
import en from "../../../../i18n/locales/en.json";

// GLOBAL — sri_tax_regime 04 is no longer selectable; a company that saved it must keep it.

const getProfile = vi.fn();
const updateFiscal = vi.fn();
vi.mock("../api/companyProfileService", () => ({
  companyProfileService: {
    getProfile: () => getProfile(),
    updateFiscal: (payload: unknown) => updateFiscal(payload),
    getFiscalPolicy: () =>
      Promise.resolve({ consumerFinalMaxAmount: 50, consumerFinalMaxAmountSource: "Manual" }),
  },
}));
vi.mock("../../../items/facades/sriLookupFacade", () => ({
  sriLookupFacade: {
    taxRegimes: () =>
      Promise.resolve([
        { code: "01", name: "Régimen General", abbrev: "GENERAL" },
        { code: "02", name: "RIMPE – Emprendedor", abbrev: "RIMPE_ME" },
        { code: "03", name: "RIMPE – Negocio Popular", abbrev: "RIMPE_NP" },
      ]),
  },
}));
vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => true }),
}));

import { FiscalSettingsSection } from "./FiscalSettingsSection";

const profile = (taxRegimeCode: string | null, overrides: Record<string, unknown> = {}) => ({
  taxRegimeCode,
  isAccountingReq: true,
  specialTaxpayerNo: "",
  isForeignTrade: false,
  withholdsRenta: false,
  withholdsVat: false,
  retentionAgentResolutionNo: null,
  retentionAgentEffectiveFrom: null,
  largeTaxpayerResolutionNo: null,
  largeTaxpayerEffectiveFrom: null,
  ...overrides,
});

async function renderWith(taxRegimeCode: string | null, overrides: Record<string, unknown> = {}) {
  getProfile.mockResolvedValue(profile(taxRegimeCode, overrides));
  render(
    <I18nProvider>
      <FiscalSettingsSection />
    </I18nProvider>,
  );
  await waitFor(() => expect(screen.queryByRole("option", { name: /RIMPE – Emprendedor/ })).not.toBeNull());
  // Todas las cargas asíncronas resueltas (la política es la última): un reset() tardío del
  // formulario no puede pisar lo que el test escribe después.
  await screen.findByText("Manual");
  return document.querySelector<HTMLSelectElement>('select[name="taxRegimeCode"]')!;
}

describe("FiscalSettingsSection — régimen no seleccionable", () => {
  afterEach(() => cleanup());

  it("conserva un régimen guardado que ya no se ofrece", async () => {
    const select = await renderWith("04");
    await waitFor(() => expect(select.value).toBe("04"));
    expect(screen.getByRole("option", { name: /04 —/ })).toBeTruthy();
  });

  it("no agrega opciones cuando el régimen guardado sigue vigente", async () => {
    const select = await renderWith("01");
    await waitFor(() => expect(select.value).toBe("01"));
    expect(select.options).toHaveLength(4);
  });
});

// R21/R22/R23 — designaciones por resolución (Anexos 21 y 24) y formato de contribuyente especial.
describe("FiscalSettingsSection — designaciones del emisor", () => {
  afterEach(() => {
    cleanup();
    updateFiscal.mockReset();
  });

  const field = (name: string) =>
    document.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
  // El primer "Guardar" es el de la configuración tributaria (el segundo, Consumidor Final).
  const save = () =>
    fireEvent.click(screen.getAllByRole("button", { name: /Guardar Configuración/ })[0]);

  it("carga las designaciones guardadas", async () => {
    await renderWith("01", {
      retentionAgentResolutionNo: "6498",
      retentionAgentEffectiveFrom: "2026-11-01",
      largeTaxpayerResolutionNo: "NAC-GCFOIOC21-00000868-E",
      largeTaxpayerEffectiveFrom: "2026-01-01",
    });

    await waitFor(() => expect(field("retentionAgentResolutionNo").value).toBe("6498"));
    expect(field("retentionAgentEffectiveFrom").value).toBe("2026-11-01");
    expect(field("largeTaxpayerResolutionNo").value).toBe("NAC-GCFOIOC21-00000868-E");
    expect(field("largeTaxpayerEffectiveFrom").value).toBe("2026-01-01");
  });

  it("exige la vigencia cuando se ingresa un número de resolución", async () => {
    await renderWith("01");

    fireEvent.change(field("retentionAgentResolutionNo"), { target: { value: "6498" } });
    save();

    expect(await screen.findByText("Indique desde qué fecha rige la resolución.")).toBeTruthy();
    expect(updateFiscal).not.toHaveBeenCalled();
  });

  it("rechaza un contribuyente especial fuera del formato del XSD", async () => {
    await renderWith("01");

    fireEvent.change(field("specialTaxpayerNo"), { target: { value: "Res. 5368" } });
    save();

    expect(
      await screen.findByText("Entre 3 y 13 caracteres alfanuméricos, sin espacios ni símbolos."),
    ).toBeTruthy();
    expect(updateFiscal).not.toHaveBeenCalled();
  });

  it("envía el número sin ceros a la izquierda y las fechas de vigencia", async () => {
    updateFiscal.mockResolvedValue(profile("01"));
    await renderWith("01");

    fireEvent.change(field("retentionAgentResolutionNo"), { target: { value: "00006498" } });
    fireEvent.change(field("retentionAgentEffectiveFrom"), { target: { value: "2026-11-01" } });
    fireEvent.change(field("largeTaxpayerResolutionNo"), {
      target: { value: "NAC-GCFOIOC21-00000868-E" },
    });
    fireEvent.change(field("largeTaxpayerEffectiveFrom"), { target: { value: "2026-01-01" } });
    save();

    await waitFor(() => expect(updateFiscal).toHaveBeenCalledTimes(1));
    expect(updateFiscal.mock.calls[0][0]).toMatchObject({
      retentionAgentResolutionNo: "6498",
      retentionAgentEffectiveFrom: "2026-11-01",
      largeTaxpayerResolutionNo: "NAC-GCFOIOC21-00000868-E",
      largeTaxpayerEffectiveFrom: "2026-01-01",
    });
  });

  it("sin designaciones envía ambos pares vacíos", async () => {
    updateFiscal.mockResolvedValue(profile("01"));
    await renderWith("01");

    fireEvent.change(field("specialTaxpayerNo"), { target: { value: "5368" } });
    save();

    await waitFor(() => expect(updateFiscal).toHaveBeenCalledTimes(1));
    expect(updateFiscal.mock.calls[0][0]).toMatchObject({
      specialTaxpayerNo: "5368",
      retentionAgentResolutionNo: null,
      retentionAgentEffectiveFrom: null,
      largeTaxpayerResolutionNo: null,
      largeTaxpayerEffectiveFrom: null,
    });
  });
});


describe("FiscalSettingsSection — indicadores independientes y ayuda", () => {
  afterEach(() => { cleanup(); updateFiscal.mockReset(); localStorage.removeItem(storageKey); });
  const names = ["isForeignTrade", "withholdsRenta", "withholdsVat"] as const;
  const input = (name: string) => document.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;

  it.each(Array.from({ length: 8 }, (_, mask) => [mask]))("conserva y guarda la combinación %i sin dependencias", async (mask) => {
    const initial = names.map((_, i) => Boolean(mask & (1 << i)));
    await renderWith("01", Object.fromEntries(names.map((name, i) => [name, initial[i]])));
    await waitFor(() => names.forEach((name, i) => expect(input(name).checked).toBe(initial[i])));
    names.forEach((name, i) => {
      const before = names.map((other) => input(other).checked);
      fireEvent.click(input(name));
      names.forEach((other, j) => expect(input(other).checked).toBe(i === j ? !before[j] : before[j]));
    });
    const stored = profile("01", Object.fromEntries(names.map((name, i) => [name, !initial[i]])));
    updateFiscal.mockImplementation(async () => { getProfile.mockResolvedValue(stored); });
    fireEvent.click(screen.getAllByRole("button", { name: /Guardar Configuraci\u00f3n/ })[0]);
    await waitFor(() => expect(updateFiscal).toHaveBeenCalledTimes(1));
    expect(updateFiscal.mock.calls[0][0]).toEqual({
      taxRegimeCode: "01", isAccountingReq: true, specialTaxpayerNo: null,
      ...Object.fromEntries(names.map((name, i) => [name, !initial[i]])),
      retentionAgentResolutionNo: null, retentionAgentEffectiveFrom: null,
      largeTaxpayerResolutionNo: null, largeTaxpayerEffectiveFrom: null,
    });
    cleanup();
    await renderWith("01", stored);
    await waitFor(() => names.forEach((name, i) => expect(input(name).checked).toBe(!initial[i])));
  });

  it.each(["es", "en"] as const)("muestra las diez ayudas en %s sin cambiar casillas", async (locale) => {
    await ensureLocaleLoaded(locale);
    localStorage.setItem(storageKey, locale);
    await renderWith("01");
    const dictionary = locale === "es" ? es : en;
    const keys = Object.entries(HELP_KEYS).filter(([key]) => key.startsWith("COMPANY_FISCAL_"));
    expect(keys).toHaveLength(10);
    for (const [, key] of keys) {
      const entry = resolveHelp(key)!;
      const title = dictionary[`${entry.i18nPrefix}.title` as keyof typeof dictionary];
      const short = dictionary[`${entry.i18nPrefix}.short` as keyof typeof dictionary];
      const long = dictionary[`${entry.i18nPrefix}.long` as keyof typeof dictionary];
      expect(title).toBeTruthy(); expect(short).toBeTruthy(); expect(long).toBeTruthy();
      fireEvent.click(screen.getByRole("button", { name: title }));
      expect(screen.getByRole("dialog").textContent).toContain(short);
      expect(screen.getByRole("dialog").textContent).toContain(long);
      fireEvent.keyDown(window, { key: "Escape" });
    }
    names.forEach((name) => expect(input(name).checked).toBe(false));
    expect(updateFiscal).not.toHaveBeenCalled();
    for (const key of [HELP_KEYS.COMPANY_FISCAL_WITHHOLDS_RENTA, HELP_KEYS.COMPANY_FISCAL_WITHHOLDS_VAT]) {
      const entry = resolveHelp(key)!;
      expect(dictionary[`${entry.i18nPrefix}.long` as keyof typeof dictionary]).toContain(
        locale === "es" ? "no acredita una designación oficial del SRI" : "does not certify official SRI",
      );
    }
  });
});
