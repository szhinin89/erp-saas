// @vitest-environment node
import { afterAll, beforeAll, expect, it, vi } from "vitest";
import { chromium, type Browser } from "@playwright/test";
import { readFileSync, mkdirSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { KardexPage } from "./KardexPage";
import { LayoutFrame } from "../../../../components/layout/LayoutFrame";

const state = vi.hoisted(() => ({ current: {} as Record<string, unknown> }));
vi.mock("../../../../access/usePermissionsUi", () => ({ usePermissionsUi: () => ({ canShow: () => true }) }));
vi.mock("../hooks/useInventoryInvestigationPage", () => ({ useInventoryInvestigationPage: () => state.current }));

vi.mock("../../../../hooks/usePrecisionPolicy", async () => {
  const { resolvePrecisionDecimals } = await import("../../../../lib/config/precisionPolicy.config");
  const { TEST_PRECISION_POLICY } = await import("../../../../test/precisionPolicyFixture");
  return { usePrecisionDecimals: (kind: Parameters<typeof resolvePrecisionDecimals>[1]) => resolvePrecisionDecimals(TEST_PRECISION_POLICY, kind) };
});
// Real React markup + production CSS in Chromium; no API, credentials or database.
const root = resolve(import.meta.dirname, "../../../../..");
function cssFile(path: string): string {
  return readFileSync(path, "utf8").replace(/@import\s+"([^"]+)";/g,
    (_, target: string) => cssFile(resolve(dirname(path), target)));
}
const pageSource = readFileSync(resolve(import.meta.dirname, "KardexPage.tsx"), "utf8");
const css = cssFile(resolve(root, "src/index.css")) + "\n" +
  [...pageSource.matchAll(/import "([^"]+\.css)";/g)]
    .map((match) => cssFile(resolve(import.meta.dirname, match[1]))).join("\n") +
    cssFile(resolve(root, "src/components/layout/LayoutFrame.css"));
const movement = {
  id: "m-1", itemId: "p-1", warehouseId: "12345678-1234-1234-1234-123456789012",
  movementType: 1, movementTypeName: "PurchaseEntry", quantity: 12000,
  uomCode: "UND", previousQuantity: 0, resultQuantity: 12000, sequenceNumber: 123,
  unitCost: 0.1218575, totalCost: 1462.29, runningAverageCost: 0.1218575,
  runningStockValue: 1462.29, effectiveDate: "2026-10-25", reference: "001-001-000000123",
  sourceDocId: "doc-1", sourceDocType: "PurchaseInvoice", createdBy: "u-1",
  createdAt: "2026-10-25T12:00:00Z", createdByName: "Administrador",
};
const detail = {
  movement, actor: { userId: "u-1", userName: "Administrador" },
  sourceDocument: { docType: "PurchaseInvoice", docNumber: movement.reference,
    partnerName: "Proveedor con nombre comercial extenso para verificar lectura",
    unitPrice: 0.1234, discountPct: 1.25, vatRate: 0, reason: null,
    notes: "Observaciones del documento de origen con información adicional." },
  documentChain: { links: [{ docType: "PurchaseInvoice", docNumber: movement.reference, isCurrent: true }] },
  relations: { current: { movementId: "m-1", sequenceNumber: 123 }, previous: null, next: null },
};
const baseline = {
  searchMode: "product", tab: "kardex", productQuery: "", productResults: [],
  selectedProduct: null, selectedProductId: "", selectedWarehouseId: "",
  warehouses: [], dateFrom: "", dateTo: "", docSubType: "PurchaseInvoice",
  docNumberQuery: "", docMatches: [], docSearching: false, selectedDoc: null,
  movements: [], movementsLoading: false, movementTypeFilter: "", summary: null,
  lastMovement: null, detailOpen: false, detailLoading: false, detail: null,
};
let browser: Browser;
beforeAll(async () => { vi.stubGlobal("localStorage", { getItem: () => null }); browser = await chromium.launch({ headless: true }); });
afterAll(async () => { await browser?.close(); vi.unstubAllGlobals(); });

for (const width of [390, 1440]) {
  for (const scenario of ["empty", "loading", "results", "document", "detail", "detail-loading", "picker-results", "picker-empty", "picker-loading", "picker-shell"]) {
    it(`${scenario} at ${width}px keeps controls and content within their regions`, async () => {
      state.current = { ...baseline };
      if (["results", "detail"].includes(scenario)) Object.assign(state.current, {
        selectedProductId: "p-1", selectedProduct: { sku: "SKU-001", shortName: "Producto de prueba" },
        movements: [movement], summary: { quantity: 12000, averageCost: 0.1218575, totalStockValue: 1462.29 },
        lastMovement: movement,
      });
      if (scenario === "loading") state.current.movementsLoading = true;
      if (scenario === "document") state.current.searchMode = "document";
      if (scenario.startsWith("picker-")) Object.assign(state.current, {
        productQuery: "producto", productSearching: scenario === "picker-loading",
        productResults: ["picker-results", "picker-shell"].includes(scenario) ? Array.from({ length: 10 }, (_, i) => ({
          id: `p-${i}`, sku: `SKU-${i.toString().padStart(3, "0")}`,
          shortName: `Producto con descripción extensa ${i + 1} ${"detalle".repeat(12)}`,
        })) : [],
      });
      if (scenario.startsWith("detail")) Object.assign(state.current, {
        detailOpen: true, detailLoading: scenario === "detail-loading", detail: scenario === "detail" ? detail : null,
      });
      const page = await browser.newPage({ viewport: { width, height: 900 } });
      try {
        await page.emulateMedia({ reducedMotion: "reduce" });
        const markup = () => renderToStaticMarkup(
          <I18nProvider><MemoryRouter>{scenario === "picker-shell"
            ? <LayoutFrame><KardexPage /></LayoutFrame> : <KardexPage />}</MemoryRouter></I18nProvider>,
        );
        const products = state.current.productResults;
        state.current.productResults = [];
        await page.setContent(`<style>${css}</style>${markup()}`);
        const originalPositions = await page.locator(".kdx-filter-row input, .kdx-filter-row select").evaluateAll(
          (els) => els.map((el) => ({ x: el.getBoundingClientRect().x, y: el.getBoundingClientRect().y })),
        );
        state.current.productResults = products;
        await page.setContent(`<style>${css}</style>${markup()}`);
        const stage = process.env.KARDEX_CAPTURE_STAGE;
        if (stage) {
          const destination = resolve(root, process.env.KARDEX_CAPTURE_DIR ?? "../docs/evidence/kardex-ux-01", stage);
          mkdirSync(destination, { recursive: true });
          await page.screenshot({ path: resolve(destination, `${scenario}-${width}.png`), fullPage: true, animations: "disabled" });
          if (scenario === "detail") {
            await page.locator(".zh-modal-body").evaluate((el) => { el.scrollTop = el.scrollHeight; });
            await page.screenshot({ path: resolve(destination, `detail-bottom-${width}.png`), animations: "disabled" });
          }
        }
        const geometry = await page.evaluate(() => {
          const filter = document.querySelector(".kdx-filter-row")!;
          const bounds = filter.getBoundingClientRect();
          const children = Array.from(filter.children).map((el) => el.getBoundingClientRect());
          const summary = document.querySelector(".kdx-summary-row");
          const cards = summary ? Array.from(summary.children).map((el) => el.getBoundingClientRect()) : [];
          const modal = document.querySelector(".zh-modal-body");
          return {
            controlsFit: children.every((r) => r.left >= bounds.left - 1 && r.right <= bounds.right + 1),
            filterHeight: bounds.height,
            summaryRows: new Set(cards.map((r) => Math.round(r.top))).size,
            modalFits: !modal || modal.scrollWidth <= modal.clientWidth + 1,
          };
        });
        expect(geometry.controlsFit).toBe(true);
        if (width === 1440) expect(geometry.filterHeight).toBeLessThan(100);
        expect(geometry.modalFits).toBe(true);
        if (scenario.startsWith("picker-")) {
          const positions = await page.locator(".kdx-filter-row input, .kdx-filter-row select").evaluateAll(
            (els) => els.map((el) => ({ x: el.getBoundingClientRect().x, y: el.getBoundingClientRect().y })),
          );
          expect(positions).toEqual(originalPositions);
          if (["picker-results", "picker-shell"].includes(scenario)) {
            const panel = page.locator(".kdx-field-product .zh-field-control > div");
            const panelGeometry = await panel.evaluate((el) => {
              const r = el.getBoundingClientRect();
              const input = el.parentElement!.querySelector("input")!.getBoundingClientRect();
              const style = getComputedStyle(el);
              const clippingAncestors = [];
              for (let parent = el.parentElement; parent; parent = parent.parentElement) {
                const s = getComputedStyle(parent);
                if ([s.overflowX, s.overflowY].some((v) => ["hidden", "clip", "auto", "scroll"].includes(v))) {
                  const bounds = parent.getBoundingClientRect();
                  if (r.left < bounds.left || r.right > bounds.right || r.bottom > bounds.bottom) clippingAncestors.push(parent.tagName);
                }
              }
              return { left: r.left, right: r.right, top: r.top, inputBottom: input.bottom,
                width: r.width, inputWidth: input.width, scrollable: el.scrollHeight > el.clientHeight,
                background: style.backgroundColor, border: parseFloat(style.borderTopWidth), clippingAncestors,
                panelOnTop: el.contains(document.elementFromPoint(r.left + r.width / 2, r.top + 20)),
                horizontalOverflow: el.scrollWidth > el.clientWidth + 1 };
            });
            expect(panelGeometry.top).toBeGreaterThanOrEqual(panelGeometry.inputBottom);
            expect(panelGeometry.width).toBeCloseTo(panelGeometry.inputWidth, 0);
            expect(panelGeometry.scrollable).toBe(true);
            expect(panelGeometry.background).not.toBe("rgba(0, 0, 0, 0)");
            expect(panelGeometry.border).toBeGreaterThan(0);
            expect(panelGeometry.clippingAncestors).toEqual([]);
            expect(panelGeometry.panelOnTop).toBe(true);
            expect(panelGeometry.left).toBeGreaterThanOrEqual(0);
            expect(panelGeometry.right).toBeLessThanOrEqual(width);
            expect(panelGeometry.horizontalOverflow).toBe(false);
            // Native button keyboard activation; React callback is covered by the component test.
            await panel.locator("button").evaluateAll((buttons) => buttons.forEach((button) => {
              button.addEventListener("click", () => button.setAttribute("data-activated", "true"));
            }));
            await page.locator(".kdx-field-product input").focus();
            await page.keyboard.press("Tab");
            await page.keyboard.press("Enter");
            expect(await panel.locator("button").first().getAttribute("data-activated")).toBe("true");
            await page.keyboard.press("Tab");
            await page.keyboard.press("Space");
            expect(await panel.locator("button").nth(1).getAttribute("data-activated")).toBe("true");
          } else {
            expect(await page.locator(".kdx-field-product .zh-field-control > div").count()).toBe(0);
          }
        }
        if (scenario === "results" && width === 1440) expect(geometry.summaryRows).toBe(1);
      } finally { await page.close(); }
    });
  }
}
