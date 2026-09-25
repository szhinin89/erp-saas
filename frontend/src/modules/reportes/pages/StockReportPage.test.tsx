// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor, within } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { StockReportPage } from "./StockReportPage";
import { stockService, type StockReportRowDto } from "../../inventory/stock/api/stockService";
import { warehouseService } from "../../inventory/warehouses/api/warehouseService";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

/**
 * ZH-LISTING-COMPLIANCE-AUDIT-08 — /reports/stock es un listado principal migrado de
 * ReportPageTemplate (ReportTable) a ZHDataTable: debe mostrar "N°" como primera columna sin
 * perder ninguna columna funcional (SKU, Producto, Bodega, Stock Actual, Disponible, Costo
 * Promedio, Valor Inventario, Estado).
 */

vi.mock("../../inventory/stock/api/stockService", () => ({
  stockService: { getReport: vi.fn() },
}));

vi.mock("../../inventory/warehouses/api/warehouseService", () => ({
  warehouseService: { list: vi.fn() },
}));

vi.mock("../../../store/authStore", () => ({
  useAuthStore: (selector: (s: { companySessionVersion: number }) => unknown) =>
    selector({ companySessionVersion: 1 }),
}));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

const ROW: StockReportRowDto = {
  productId: "prod-1",
  sku: "SKU-001",
  productName: "Arroz Superior",
  warehouseId: "wh-1",
  warehouseName: "Bodega Central",
  quantity: 100,
  availableQuantity: 90,
  averageCost: 1.5,
  stockValue: 150,
  status: "Disponible",
};

function renderPage() {
  return render(
    <I18nProvider>
      <StockReportPage />
    </I18nProvider>,
  );
}

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(warehouseService.list).mockResolvedValue([]);
  vi.mocked(stockService.getReport).mockResolvedValue([ROW]);
});

describe("StockReportPage — ZH-LISTING-COMPLIANCE-AUDIT-08", () => {
  it('muestra "N°" como primera columna', async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("SKU-001")).toBeTruthy());

    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers[0]).toBe("N°");
  });

  it("la primera fila muestra 1 en la columna N°", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("SKU-001")).toBeTruthy());

    const rows = screen.getAllByRole("row").slice(1);
    const firstCell = within(rows[0]).getAllByRole("cell")[0];
    expect(firstCell.textContent).toBe("1");
  });

  it("conserva las columnas funcionales: SKU, producto, bodega y estado", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("SKU-001")).toBeTruthy());

    expect(screen.getByText("Arroz Superior")).toBeTruthy();
    expect(screen.getByText("Bodega Central")).toBeTruthy();
    expect(screen.getAllByText("Disponible").length).toBeGreaterThan(0);
  });

  it("sincroniza document.title con el título del reporte (ZH-APP-PAGE-SHELL-STANDARD-01)", async () => {
    renderPage();
    await screen.findByText("SKU-001");

    expect(document.title).toBe("Reporte de Stock");
  });
});

// ZH-DESIGN-SYSTEM-PRECISION-02B — Stock Actual / Disponible / Unidades Totales (quantity) y
// Costo Promedio (averageCost) salen de la PrecisionPolicy; sin los literales 4 / 6 anteriores.
describe("StockReportPage — precisión semántica (02B)", () => {
  const PRECISE_ROW: StockReportRowDto = {
    ...ROW,
    quantity: 12.3456789,
    availableQuantity: 10.5,
    averageCost: 1.23456789,
  };

  async function renderWithPolicy(quantityDecimals: number, averageCostDecimals: number) {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals, averageCostDecimals });
    vi.mocked(stockService.getReport).mockResolvedValue([PRECISE_ROW]);
    renderPage();
    await waitFor(() => expect(screen.getByText("SKU-001")).toBeTruthy());
    const row = screen.getByText("SKU-001").closest("tr")!;
    const [quantity, available, averageCost] = [...row.querySelectorAll(".zh-number-value")].map(
      (el) => el.textContent,
    );
    return { quantity, available, averageCost };
  }

  it("policy quantity=2 / averageCost=4", async () => {
    const cells = await renderWithPolicy(2, 4);
    expect(cells).toEqual({ quantity: "12.35", available: "10.50", averageCost: "1.2346" });
    expect(screen.getByText("Unidades Totales").closest(".pg-kpi")?.textContent).toContain("12.35");
  });

  it("policy quantity=6 / averageCost=8", async () => {
    const cells = await renderWithPolicy(6, 8);
    expect(cells).toEqual({ quantity: "12.345679", available: "10.500000", averageCost: "1.23456789" });
    expect(screen.getByText("Unidades Totales").closest(".pg-kpi")?.textContent).toContain("12.345679");
  });

  it("Valor Inventario (money) no se migra en este piloto: sigue en 2 decimales", async () => {
    await renderWithPolicy(6, 8);
    const row = screen.getByText("SKU-001").closest("tr")!;
    expect(within(row).getByText("150.00")).toBeTruthy();
  });
});

// ZH-DESIGN-SYSTEM-PRECISION-04F — "Valor Inventario" (celda y KPI) deja el default legacy 2 de
// `formatMoney(x)`: declara money (valor monetario del stock), sin "$" como antes, reactivo.
describe("StockReportPage — valor de inventario money (04F)", () => {
  it("celda y KPI usan moneyDecimals, sin símbolo, y reaccionan A → B", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    vi.mocked(stockService.getReport).mockResolvedValue([{ ...ROW, stockValue: 150.125 }]);
    const { container } = renderPage();
    await waitFor(() => expect(screen.getByText("SKU-001")).toBeTruthy());
    const row = screen.getByText("SKU-001").closest("tr")!;
    const cell = () => [...row.querySelectorAll(".zh-number-value")].map((e) => e.textContent);
    const kpi = () => [...container.querySelectorAll(".pg-kpi-value .zh-number-value")].map((e) => e.textContent);
    expect(cell()).toContain("150.13");
    expect(kpi()).toContain("150.13");
    expect(container.textContent).not.toContain("$150");

    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 }));
    expect(cell()).toContain("150.125");
    expect(kpi()).toContain("150.125");
  });
});
