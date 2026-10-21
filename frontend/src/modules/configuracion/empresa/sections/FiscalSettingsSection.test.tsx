// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";

// GLOBAL — sri_tax_regime 04 is no longer selectable; a company that saved it must keep it.

const getProfile = vi.fn();
vi.mock("../api/companyProfileService", () => ({
  companyProfileService: {
    getProfile: () => getProfile(),
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

const profile = (taxRegimeCode: string | null) => ({
  taxRegimeCode,
  isAccountingReq: true,
  specialTaxpayerNo: "",
  isForeignTrade: false,
  withholdsRenta: false,
  withholdsVat: false,
});

async function renderWith(taxRegimeCode: string | null) {
  getProfile.mockResolvedValue(profile(taxRegimeCode));
  render(
    <I18nProvider>
      <FiscalSettingsSection />
    </I18nProvider>,
  );
  await waitFor(() => expect(screen.queryByRole("option", { name: /RIMPE – Emprendedor/ })).not.toBeNull());
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
