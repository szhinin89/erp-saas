// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider, useI18n } from "../../../i18n/i18n";
import { ensureLocaleLoaded, storageKey } from "../../../i18n/dictionaries";
import { DashboardPage } from "./DashboardPage";
import type { DashboardKpisDto } from "../api/dashboardService";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04F — `fmt` del dashboard ya no tiene default legacy 2: todos sus
 * usos son montos (ventas, CxC, CxP) y reciben la escala de money por semántica declarada. Los
 * conteos (`fmtN`) no cambian.
 */

const KPIS = {
  salesMtd: 1234.5,
  invoicesMtd: 7,
  salesYtd: 9876.125,
  pendingArTotal: 50,
  pendingArCount: 1,
  overdueArTotal: -2.5,
  overdueArCount: 1,
  pendingApTotal: 0,
  pendingApCount: 0,
  overdueApTotal: 0,
  overdueApCount: 0,
  lowStockSkuCount: 3,
  outOfStockSkuCount: 1,
  asOf: "2026-09-25",
  month: 9,
  year: 2026,
} satisfies DashboardKpisDto;

vi.mock("../hooks/useDashboardData", () => ({
  useDashboardKpis: () => ({ data: KPIS, loading: false, error: null }),
}));

afterEach(() => { cleanup(); localStorage.clear(); });

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

function LanguageControls() {
  const { setLocale } = useI18n();
  return <><button onClick={() => setLocale("en")}>English</button><button onClick={() => setLocale("es")}>Español</button></>;
}

describe("DashboardPage — montos con semántica money (04F)", () => {
  it("cambia español → inglés → español sin remontar ni alterar los importes", async () => {
    setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
    const { container } = render(<I18nProvider><MemoryRouter><LanguageControls /><DashboardPage /></MemoryRouter></I18nProvider>);
    expect(container.textContent).toContain("Ventas del mes");
    fireEvent.click(screen.getByRole("button", { name: "English" }));
    await waitFor(() => expect(container.textContent).toContain("Monthly sales"));
    expect(container.textContent).toContain("$1234.50");
    expect(container.textContent).not.toContain("Ventas del mes");
    fireEvent.click(screen.getByRole("button", { name: "Español" }));
    expect(container.textContent).toContain("Ventas del mes");
  });
  it.each([
    ["es", "Ventas del mes", "7 facturas", "Resumen Anual", "Cuentas por cobrar vencidas"],
    ["en", "Monthly sales", "7 invoices", "Annual summary", "Overdue accounts receivable"],
  ] as const)("traduce los KPIs y resumen en %s", async (locale, sales, invoices, summary, overdue) => {
    await ensureLocaleLoaded(locale);
    localStorage.setItem(storageKey, locale);
    setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
    const { container } = renderPage();
    for (const label of [sales, invoices, summary, overdue]) expect(container.textContent).toContain(label);
    if (locale === "en") {
      for (const spanish of ["Ventas del mes", "facturas", "vencido", "sin stock", "Antigüedad", "No hay cuentas", "derechos reservados"])
        expect(container.textContent).not.toContain(spanish);
    }
  });
  it("montos con $ y escala money; reacciona A → B sin remount; cero/negativo conservan contrato", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    const { container } = renderPage();
    const text = () => container.textContent ?? "";
    expect(text()).toContain("$1234.50");
    expect(text()).toContain("$9876.13");
    expect(text()).toContain("$-2.50 vencido");
    expect(text()).toContain("$0.00");
    expect(text()).toContain("7 facturas");

    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 }));
    expect(text()).toContain("$1234.500");
    expect(text()).toContain("$9876.125");
    expect(text()).toContain("$-2.500 vencido");
    expect(text()).toContain("7 facturas");
  });
});
