// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { SalesReportPage } from "./SalesReportPage";
import { salesService, type SalesReportRowDto } from "../../sales/api/salesService";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04F — el reporte de ventas ya no usa `formatMoney(x)` (default 2):
 * Subtotal/Descuento/Total declaran money e IVA declara tax (no todo es money), sin símbolo "$"
 * como antes, y reaccionan a la policy sin remount.
 */

vi.mock("../../sales/api/salesService", () => ({ salesService: { dailyReport: vi.fn() } }));
vi.mock("../../../store/authStore", () => ({
  useAuthStore: (selector: (s: { companySessionVersion: number }) => unknown) =>
    selector({ companySessionVersion: 1 }),
}));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

const ROW: SalesReportRowDto = {
  id: "inv-1",
  invoiceNumber: "001-001-000000001",
  issueDate: "2026-09-25",
  customerId: "c-1",
  customerName: "Cliente Uno",
  subtotal: 100.5,
  totalVat: 15.075,
  totalDiscount: 1.25,
  grandTotal: 114.325,
  status: "Authorized",
  emissionType: "Electronic",
};

afterEach(() => cleanup());

describe("SalesReportPage — money vs tax (04F)", () => {
  it("IVA usa taxDecimals y el resto moneyDecimals; sin $; A → B sin remount", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2, taxDecimals: 4 });
    vi.mocked(salesService.dailyReport).mockResolvedValue({
      items: [ROW],
      totals: { count: 1, subtotal: 100.5, totalVat: 15.075, totalDiscount: 1.25, grandTotal: 114.325 },
    } as Awaited<ReturnType<typeof salesService.dailyReport>>);
    const { container } = render(
      <I18nProvider>
        <SalesReportPage />
      </I18nProvider>,
    );
    await waitFor(() => expect(screen.getByText("Cliente Uno")).toBeTruthy());
    const cells = () =>
      [...screen.getByText("Cliente Uno").closest("tr")!.querySelectorAll(".zh-number-value")].map(
        (e) => e.textContent,
      );
    expect(cells()).toEqual(["100.50", "15.0750", "1.25", "114.33"]);
    expect(container.textContent).toContain("Descuento total: 1.25");
    expect(container.querySelector(".zh-money-value")).toBeNull();

    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3, taxDecimals: 2 }));
    expect(cells()).toEqual(["100.500", "15.08", "1.250", "114.325"]);
    expect(container.textContent).toContain("Descuento total: 1.250");
  });
});
