// @vitest-environment jsdom
import { afterAll, afterEach, beforeAll, beforeEach, expect, it, vi } from "vitest";
import { act, cleanup, render } from "@testing-library/react";
import { chromium, type Browser } from "@playwright/test";
import { readFileSync, mkdirSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { I18nProvider } from "../../../i18n/i18n";
import { LayoutFrame } from "../../../components/layout/LayoutFrame";
import { StockReportPage } from "./StockReportPage";
import { stockLookupFacade } from "../../inventory/facades/stockLookupFacade";

vi.mock("../../inventory/facades/stockLookupFacade", () => ({ stockLookupFacade: { getReport: vi.fn() } }));
vi.mock("../../inventory/facades/warehouseLookupFacade", () => ({ warehouseLookupFacade: { list: async () => [] } }));
vi.mock("../../../store/authStore", () => ({ useAuthStore: (select: (s: { companySessionVersion: number }) => unknown) => select({ companySessionVersion: 1 }) }));
vi.mock("../../../lib/messages", () => ({ message: { error: vi.fn() } }));

const root = resolve(import.meta.dirname, "../../../..");
function cssFile(path: string): string {
  return readFileSync(path, "utf8").replace(/^\uFEFF/, "").replace(/@import\s+"([^"]+)";/g,
    (_, target: string) => cssFile(resolve(dirname(path), target)));
}
const source = readFileSync(resolve(import.meta.dirname, "StockReportPage.tsx"), "utf8");
const css = ["src/index.css", "src/components/ReportPageTemplate.css", "src/components/layout/LayoutFrame.css"]
  .map((path) => cssFile(resolve(root, path))).join("\n") +
  [...source.matchAll(/import "([^"]+\.css)";/g)].map((m) => cssFile(resolve(import.meta.dirname, m[1]))).join("\n");
let browser: Browser;
beforeAll(async () => { browser = await chromium.launch({ headless: true }); });
afterAll(async () => { await browser?.close(); });
afterEach(() => cleanup());
beforeEach(() => vi.clearAllMocks());

for (const width of [390, 1440]) for (const scenario of ["empty", "loading", "results", "error"]) {
  it(`${scenario} at ${width}px uses the available space without overflow`, async () => {
    const getReport = vi.mocked(stockLookupFacade.getReport);
    if (scenario === "loading") getReport.mockImplementation(() => new Promise(() => {}));
    else if (scenario === "error") getReport.mockRejectedValue(new Error("Fallo simulado"));
    else getReport.mockResolvedValue(scenario === "empty" ? [] : [{
      productId: "p-1", warehouseId: "w-1", sku: "SKU-001", productName: "Producto de prueba",
      warehouseName: "Bodega Central", quantity: 12000, availableQuantity: 11900,
      averageCost: 0.1218575, stockValue: 1462.29, status: "Disponible",
    }]);
    let container!: HTMLElement;
    await act(async () => {
      container = render(<I18nProvider><LayoutFrame><StockReportPage /></LayoutFrame></I18nProvider>).container;
    });
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    try {
      await page.emulateMedia({ reducedMotion: "reduce" });
      await page.setContent(`<style>${css}</style>${container.innerHTML}`);
      const stage = process.env.STOCK_CAPTURE_STAGE;
      if (stage) {
        const destination = resolve(root, "../docs/evidence/stock-report-ux-01", stage);
        mkdirSync(destination, { recursive: true });
        await page.screenshot({ path: resolve(destination, `${scenario}-${width}.png`), fullPage: true, animations: "disabled" });
      }
      const geometry = await page.evaluate(() => {
        const grid = document.querySelector(".pg-kpis")!.getBoundingClientRect();
        const cards = Array.from(document.querySelectorAll(".pg-kpi")).map((el) => el.getBoundingClientRect());
        const fields = document.querySelector(".rpt-filters-fields")!.getBoundingClientRect();
        const inputs = Array.from(document.querySelectorAll(".rpt-filter-field input, .rpt-filter-field select")).map((el) => el.getBoundingClientRect());
        const actions = document.querySelector(".rpt-filters-actions")!.getBoundingClientRect();
        const empty = document.querySelector(".empty-state");
        return { unusedKpiWidth: grid.right - cards[2].right,
          kpiRows: new Set(cards.map((r) => r.top)).size,
          unusedFieldWidth: fields.right - inputs[1].right,
          inputsAligned: Math.abs(inputs[0].bottom - inputs[1].bottom) < 1,
          actionsAligned: Math.abs(inputs[0].bottom - actions.bottom) < 3,
          pageFits: document.documentElement.scrollWidth <= window.innerWidth + 1,
          emptyHeight: empty?.parentElement?.getBoundingClientRect().height ?? 0,
          emptyCentered: !empty || getComputedStyle(empty).textAlign === "center" };
      });
      expect(geometry.pageFits).toBe(true);
      expect(geometry.unusedKpiWidth).toBeLessThan(2);
      expect(geometry.kpiRows).toBe(width === 1440 ? 1 : 3);
      if (width === 1440) {
        expect(geometry.unusedFieldWidth).toBeLessThan(2);
        expect(geometry.inputsAligned).toBe(true);
        expect(geometry.actionsAligned).toBe(true);
      }
      if (scenario === "empty") {
        expect(geometry.emptyHeight).toBeLessThan(160);
        expect(geometry.emptyCentered).toBe(true);
      }
      if (scenario === "loading") expect(await page.locator(".loading-state").count()).toBe(1);
      if (scenario === "results") expect(await page.getByText("SKU-001").count()).toBe(1);
      if (scenario === "error") expect(await page.getByText("Error", { exact: true }).count()).toBe(1);
    } finally { await page.close(); }
  });
}
