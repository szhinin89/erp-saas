// @vitest-environment node
import { afterAll, beforeAll, expect, it, vi } from "vitest";
import { chromium, type Browser } from "@playwright/test";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
import { ZHDataTable } from "../../../components/zh/ZHDataTable";
import { PurchaseReceptionFilters } from "./PurchaseReceptionFilters";
import { PurchaseReceptionProcessCell } from "./PurchaseReceptionProcessCell";
import { EMPTY_RECEPTION_FILTERS } from "../utils/receptionFilters";
import { buildReceptionRow } from "../utils/reception.testFixtures";

// SSR sin store de PrecisionPolicy: los inputs de moneda solo necesitan la escala para render.
vi.mock("../../../hooks/usePrecisionPolicy", () => ({ usePrecisionDecimals: () => 2 }));

/**
 * PURCHASES-RECEPTION-FILTERS-01 — revisión visual focalizada (mismo patrón que
 * InternalCapabilities.visual.test): componentes reales + CSS real en Chromium a 390 y 1440 px,
 * con la zona avanzada plegada y desplegada. Capturas en .local/purchases-reception-filters/.
 */
const root = resolve(import.meta.dirname, "../../../..");
const output = resolve(root, "../.local/purchases-reception-filters/visual");
function cssFile(file: string): string {
  // Sin BOM: al concatenar hojas, un BOM intermedio invalida el primer selector de la siguiente.
  return readFileSync(file, "utf8").replace(/^\uFEFF/, "").replace(/@import\s+"([^"]+)";/g, (_, target: string) =>
    cssFile(resolve(dirname(file), target)),
  );
}
const css = [
  "src/index.css",
  "src/components/zh/inputs/ZhInputs.css",
  "src/components/zh/help/zh-help.css",
  "src/modules/purchases/styles/purchase-reception.css",
]
  .map((file) => cssFile(resolve(root, file)))
  .join("\n");
const fonts = [...readFileSync(resolve(root, "index.html"), "utf8").matchAll(/<link[\s\S]*?>/g)]
  .map((m) => m[0])
  .filter((tag) => tag.includes("fonts."))
  .join("\n");

const rows = [
  buildReceptionRow({ documentId: "s", supplierName: "XML almacenado", documentStatus: "VERIFIED", processingStatus: "PROCESSED" }),
  buildReceptionRow({ documentId: "p", supplierName: "XML pendiente" }),
  buildReceptionRow({ documentId: "l", supplierName: "Consultando" }),
  buildReceptionRow({ documentId: "e", supplierName: "Error en sesión" }),
];
const session: Record<string, "loading" | "error"> = { l: "loading", e: "error" };

function markup(advancedOpen: boolean) {
  return renderToStaticMarkup(
    <I18nProvider>
      <MemoryRouter>
        <main className="pg-section">
          <div className="pur-reception-top">
            <div className="pur-reception-kpis">
              <ReportKpiCard layout="horizontal" icon="check_circle" tone="success" label="Registrados en ERP" value="12" onClick={() => {}} active={false} />
              <ReportKpiCard layout="horizontal" icon="hourglass_empty" tone="neutral" label="Por procesar" value="30" onClick={() => {}} active />
              <ReportKpiCard layout="horizontal" icon="person_add" tone="warning" label="Pendientes de proveedor" value="4" onClick={() => {}} active={false} />
            </div>
          </div>
          <PurchaseReceptionFilters
            filters={{ ...EMPTY_RECEPTION_FILTERS, erpSituation: "toProcess", docType: "INVOICE" }}
            setFilter={() => {}}
            advancedFilterCount={1}
            supplierOptions={[{ ruc: "0999999999001", name: "Proveedor Uno" }]}
            selectedSupplierOptions={[]}
            setSupplierFilter={() => {}}
            dateSortOrder="desc"
            setDateSortOrder={() => {}}
            clearFilters={() => {}}
            shownCount={30}
            totalCount={46}
            defaultAdvancedOpen={advancedOpen}
          />
          <ZHDataTable
            columns={[
              { key: "supplier", header: "Proveedor", render: (row) => row.supplierName },
              {
                key: "process",
                header: "Recepción y XML",
                render: (row) => <PurchaseReceptionProcessCell row={row} xmlState={session[row.documentId]} />,
              },
            ]}
            rows={rows}
            rowKey={(row) => row.documentId}
          />
        </main>
      </MemoryRouter>
    </I18nProvider>,
  );
}

let browser: Browser;
beforeAll(async () => {
  vi.stubGlobal("localStorage", { getItem: () => null });
  mkdirSync(output, { recursive: true });
  browser = await chromium.launch({ headless: true });
});
// Posición de la cabecera de la tabla por escenario (comparación antes/después del ajuste compacto).
const metrics: Record<string, { tableTop: number; theadBackground: string }> = {};
afterAll(async () => {
  writeFileSync(resolve(output, "metrics.json"), JSON.stringify(metrics, null, 2));
  await browser?.close();
  vi.unstubAllGlobals();
});

for (const width of [390, 768, 1440]) {
  for (const advancedOpen of [false, true]) {
    it(`recepción: KPI + filtros ${advancedOpen ? "desplegados" : "plegados"} a ${width}px`, async () => {
      const page = await browser.newPage({ viewport: { width, height: 900 } });
      try {
        await page.setContent(
          `<!doctype html><html><head><meta charset="utf-8">${fonts}<style>${css}</style></head><body>${markup(advancedOpen)}</body></html>`,
          { waitUntil: "networkidle" },
        );
        await page.evaluate(() => document.fonts.ready);
        const overflow = await page.evaluate(
          () => document.documentElement.scrollWidth > window.innerWidth,
        );
        expect(overflow, "sin scroll horizontal de página").toBe(false);
        expect(await page.locator(".pg-kpi--active").count()).toBe(1);
        expect(await page.locator('[aria-pressed="true"]').count()).toBe(1);
        expect(await page.locator(".pur-reception-filters__advanced").count()).toBe(advancedOpen ? 1 : 0);
        expect(await page.getByText("Mostrando 30 de 46 comprobantes").count()).toBe(1);
        for (const label of ["Almacenado", "Pendiente", "Consultando...", "Error en esta sesión"]) {
          expect(await page.locator(".pur-process-cell").getByText(label, { exact: true }).count()).toBe(1);
        }
        // Ajuste compacto: atención como checkbox del DS (sin tarjeta) y línea 1 sin rótulos.
        expect(await page.locator(".zh-toggle").count()).toBe(0);
        expect(await page.getByRole("checkbox", { name: "Solo requieren atención" }).isVisible()).toBe(true);
        expect(await page.locator(".zh-filterbar .zh-field-label").count()).toBe(advancedOpen ? 10 : 0);
        if (advancedOpen) {
          const zone = await page.locator(".pur-reception-filters__advanced").boundingBox();
          expect(zone!.width, "la zona avanzada ocupa su propia fila").toBeGreaterThan(width * 0.6);
        }
        const thead = page.locator(".zh-table thead, table thead").first();
        metrics[`${width}-${advancedOpen ? "open" : "closed"}`] = {
          tableTop: Math.round((await thead.boundingBox())!.y),
          theadBackground: await thead.evaluate((el) => getComputedStyle(el.querySelector("th") ?? el).backgroundColor),
        };
        await page.screenshot({
          path: resolve(output, `reception-filters-${width}-${advancedOpen ? "open" : "closed"}.png`),
          fullPage: true,
        });
      } finally {
        await page.close();
      }
    }, 60000);
  }
}
