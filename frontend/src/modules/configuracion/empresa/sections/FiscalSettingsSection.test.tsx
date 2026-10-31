// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, waitFor, fireEvent } from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";

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
