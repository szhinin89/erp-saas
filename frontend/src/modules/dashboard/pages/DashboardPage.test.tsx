// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
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
  asOf: "2026-09-25T00:00:00Z",
  month: 9,
  year: 2026,
} satisfies DashboardKpisDto;

vi.mock("../hooks/useDashboardData", () => ({
  useDashboardKpis: () => ({ data: KPIS, loading: false, error: null }),
}));

afterEach(() => cleanup());

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("DashboardPage — montos con semántica money (04F)", () => {
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
