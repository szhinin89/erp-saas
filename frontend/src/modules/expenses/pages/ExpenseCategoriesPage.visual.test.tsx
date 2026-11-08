// @vitest-environment node
import { afterAll, beforeAll, expect, it, vi } from "vitest";
import { chromium, type Browser } from "@playwright/test";
import { mkdirSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import type { AccountDto } from "../../accounting/facades/accountLookupFacade";
import type { ExpenseCategoryTreeNodeDto } from "../api/expenseCategoryService";
import { ExpenseCategoryFormPanel } from "../components/ExpenseCategoryFormPanel";
import { ExpenseCategoryTree } from "../components/ExpenseCategoryTree";

// UIUX-EXPENSE-CATEGORIES-MODAL — markup React real + CSS de producción en Chromium;
// sin API, credenciales ni base de datos. Capturas opcionales con EXPENSE_CAT_CAPTURE_DIR.
const root = resolve(import.meta.dirname, "../../../..");
function cssFile(path: string): string {
  return readFileSync(path, "utf8").replace(/@import\s+"([^"]+)";/g,
    (_, target: string) => cssFile(resolve(dirname(path), target)));
}
const css = cssFile(resolve(root, "src/index.css")) + "\n" +
  cssFile(resolve(import.meta.dirname, "../styles/expense-categories.css"));

const account: AccountDto = {
  id: "acc-1", code: "5.1.01.001", name: "Energía eléctrica y servicios básicos del local",
  isActive: true, allowsPosting: true, accountType: "Expense",
} as AccountDto;
const accountsById = new Map([[account.id, account]]);

function node(
  id: string, level: ExpenseCategoryTreeNodeDto["level"], parentId: string | null,
  code: string, name: string, children: ExpenseCategoryTreeNodeDto[] = [],
  description: string | null = null,
): ExpenseCategoryTreeNodeDto {
  return {
    id, companyId: "c-1", parentId, level, code, name, description,
    accountingAccountId: level === "Subcategory" ? account.id : null,
    isActive: id !== "t3", children,
  };
}

const tree = ["1", "2", "3"].map((t) =>
  node(`t${t}`, "Type", null, `T0${t}`, `Gastos operativos y administrativos ${t}`,
    ["1", "2", "3"].map((c) =>
      node(`c${t}${c}`, "Category", `t${t}`, `C0${t}${c}`, `Servicios del establecimiento ${c}`,
        ["1", "2", "3"].map((s) =>
          node(`s${t}${c}${s}`, "Subcategory", `c${t}${c}`, `S${t}${c}${s}`,
            `Subcategoría con nombre extenso para verificar recorte ${s}`, [],
            s === "1" ? "Consumo mensual facturado por la empresa eléctrica a la sucursal matriz" : null)))),
    t === "1" ? "Gastos recurrentes de operación" : null));

const noop = () => {};

function markup(scenario: "tree" | "modal") {
  return renderToStaticMarkup(
    <I18nProvider>
      <MemoryRouter>
        <PageShell kicker="Gastos" title="Catálogo de gastos"
          subtitle="Administra la jerarquía Tipo de gasto, Categoría y Subcategoría con cuenta contable destino.">
          <ZHCard bodyClassName="exp-cat-card-body">
            <ExpenseCategoryTree nodes={tree} accountsById={accountsById} selectedId="s331"
              collapsedIds={new Set(["c12", "c13", "t2"])} canCreate canUpdate canActivate canDeactivate
              onSelect={noop} onToggleExpanded={noop} onExpandAll={noop} onCollapseAll={noop}
              onCreateType={noop} onCreateChild={noop} onEdit={noop} onToggleActive={noop} />
          </ZHCard>
          {scenario === "modal" && (
            <ZHModal open size="md" title="Crear subcategoría" closeOnBackdrop={false} onClose={noop}
              subtitle="Dentro de: T03 · Gastos operativos y administrativos 3 › C033 · Servicios del establecimiento 3"
              footer={<><ZHBtn type="button" variant="ghost">Cancelar</ZHBtn>
                <ZHBtn type="submit" variant="primary" form="f">Guardar</ZHBtn></>}>
              <ExpenseCategoryFormPanel formId="f" level="Subcategory" accounts={[account]} saving={false}
                form={{ code: "S334", name: "Agua potable", description: "", accountingAccountId: "" }}
                fieldErrors={{ accountingAccountId: "La subcategoría requiere una cuenta contable." }}
                onChange={noop} onSubmit={noop} />
            </ZHModal>
          )}
        </PageShell>
      </MemoryRouter>
    </I18nProvider>,
  );
}

let browser: Browser;
beforeAll(async () => {
  vi.stubGlobal("localStorage", { getItem: () => null });
  browser = await chromium.launch({ headless: true });
});
afterAll(async () => { await browser?.close(); vi.unstubAllGlobals(); });

for (const width of [1280, 768, 320]) {
  for (const scenario of ["tree", "modal"] as const) {
    it(`${scenario} a ${width}px: sin scroll horizontal y contenido dentro del viewport`, async () => {
      const page = await browser.newPage({ viewport: { width, height: 720 } });
      try {
        await page.emulateMedia({ reducedMotion: "reduce" });
        await page.setContent(`<style>${css}</style>${markup(scenario)}`);
        const captureDir = process.env.EXPENSE_CAT_CAPTURE_DIR;
        if (captureDir) {
          mkdirSync(captureDir, { recursive: true });
          await page.screenshot({ path: resolve(captureDir, `${scenario}-${width}.png`),
            fullPage: scenario === "tree", animations: "disabled" });
        }
        const g = await page.evaluate(() => {
          const rows = Array.from(document.querySelectorAll<HTMLElement>(".exp-cat-node"))
            .filter((el) => el.offsetParent !== null);
          const modal = document.querySelector(".zh-modal")?.getBoundingClientRect();
          const body = document.querySelector(".zh-modal-body");
          return {
            pageOverflow: document.documentElement.scrollWidth > window.innerWidth,
            rowsInside: rows.every((el) => {
              const r = el.getBoundingClientRect();
              return r.left >= 0 && r.right <= window.innerWidth;
            }),
            maxRowHeight: Math.max(...rows.map((el) => el.getBoundingClientRect().height)),
            hiddenRendered: document.querySelectorAll(".exp-cat-node__children[hidden] .exp-cat-node")
              .length > 0,
            hiddenVisible: Array.from(document.querySelectorAll<HTMLElement>(
              ".exp-cat-node__children[hidden] .exp-cat-node")).some((el) => el.offsetParent !== null),
            modal: modal && { top: modal.top, bottom: modal.bottom, left: modal.left, right: modal.right },
            modalBodyFits: !body || body.scrollWidth <= body.clientWidth + 1,
          };
        });
        expect(g.pageOverflow).toBe(false);
        expect(g.rowsInside).toBe(true);
        expect(g.hiddenRendered).toBe(true);
        expect(g.hiddenVisible).toBe(false);
        // Fila compacta: antes min-height 64px + padding; en escritorio queda bajo 64px.
        if (width >= 768) expect(g.maxRowHeight).toBeLessThan(64);
        if (scenario === "modal") {
          expect(g.modal).toBeTruthy();
          expect(g.modal!.top).toBeGreaterThanOrEqual(0);
          expect(g.modal!.bottom).toBeLessThanOrEqual(720);
          expect(g.modal!.left).toBeGreaterThanOrEqual(0);
          expect(g.modal!.right).toBeLessThanOrEqual(width);
          expect(g.modalBodyFits).toBe(true);
        }
      } finally {
        await page.close();
      }
    }, 30_000);
  }
}
