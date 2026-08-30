// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { StockReportPage } from "./StockReportPage";
import { stockService, type StockReportRowDto } from "../../inventory/stock/api/stockService";
import { warehouseService } from "../../inventory/warehouses/api/warehouseService";

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
});
