// @vitest-environment jsdom
import { afterAll, afterEach, beforeAll, beforeEach, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { chromium, type Browser } from "@playwright/test";
import { mkdirSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { I18nProvider } from "../../../i18n/i18n";
import { LayoutFrame } from "../../../components/layout/LayoutFrame";
import { ChartOfAccountsPage } from "./ChartOfAccountsPage";
import { accountingApi, type AccountDto } from "../api/accountingApi";
import { account } from "./ChartOfAccountsPage.testFixtures";

// ACCOUNTING-CHART-UX-01 — página real (jsdom) + CSS de producción en Chromium; sin API,
// credenciales ni base de datos. Capturas opcionales con COA_CAPTURE_DIR.
vi.mock("../api/accountingApi", () => ({ accountingApi: { listAccounts: vi.fn() } }));
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => true, has: () => true, isAdminRole: false }),
}));
vi.mock("../../../lib/messages", () => ({ message: { error: vi.fn(), success: vi.fn(), confirm: vi.fn() } }));

const root = resolve(import.meta.dirname, "../../../..");
function cssFile(path: string): string {
  return readFileSync(path, "utf8").replace(/^\uFEFF/, "").replace(/@import\s+"([^"]+)";/g,
    (_, target: string) => cssFile(resolve(dirname(path), target)));
}
const css = ["src/index.css", "src/components/ReportPageTemplate.css", "src/components/layout/LayoutFrame.css",
  "src/styles/shared/items-catalog.css", "src/modules/accounting/pages/ChartOfAccountsPage.css"]
  .map((path) => cssFile(resolve(root, path))).join("\n");

/** Plan realista: 4 niveles, nombres largos, agrupadoras/movimiento e inactivas. */
function chart(): AccountDto[] {
  const list: AccountDto[] = [];
  const add = (code: string, name: string, parent: AccountDto | null, extra: Partial<AccountDto> = {}) => {
    const created = account({
      id: `id-${code}`, code, name, allowsPosting: code.split(".").length === 4,
      parentAccountId: parent?.id ?? null, parentAccountCode: parent?.code ?? null,
      parentAccountName: parent?.name ?? null, ...extra,
    });
    list.push(created);
    return created;
  };
  const roots = [["1", "Activo"], ["2", "Pasivo"], ["3", "Patrimonio neto"]] as const;
  for (const [code, name] of roots) {
    const r = add(code, name, null, { allowsPosting: false });
    for (const g of ["1", "2"]) {
      const g1 = add(`${code}.${g}`, `${name} corriente y no corriente ${g}`, r, { allowsPosting: false });
      const g2 = add(`${code}.${g}.01`, "Efectivo, equivalentes de efectivo y activos financieros", g1, { allowsPosting: false });
      for (const l of ["001", "002", "003"]) {
        add(`${code}.${g}.01.${l}`, `Cuenta de movimiento con nombre extenso para validar recorte ${l}`, g2,
          { isActive: l !== "003" });
      }
    }
  }
  return list;
}

let browser: Browser;
beforeAll(async () => { browser = await chromium.launch({ headless: true }); });
afterAll(async () => { await browser?.close(); });
beforeEach(() => vi.mocked(accountingApi.listAccounts).mockResolvedValue(chart()));
afterEach(() => cleanup());

type Scenario = "initial" | "expanded" | "search" | "modal";

for (const width of [390, 768, 1440]) for (const scenario of ["initial", "expanded", "search", "modal"] as Scenario[]) {
  it(`${scenario} a ${width}px: sin scroll horizontal de página y controles dentro del viewport`, async () => {
    let container!: HTMLElement;
    await act(async () => {
      container = render(<I18nProvider><LayoutFrame><ChartOfAccountsPage /></LayoutFrame></I18nProvider>).container;
    });
    await waitFor(() => expect(screen.getByText("Patrimonio neto")).toBeTruthy());
    if (scenario === "expanded") fireEvent.click(screen.getByRole("button", { name: /Expandir todo/ }));
    if (scenario === "search")
      fireEvent.change(screen.getByPlaceholderText("Código o nombre..."), { target: { value: "2.2.01.002" } });
    if (scenario === "modal") {
      fireEvent.click(screen.getByRole("button", { name: /Expandir todo/ }));
      fireEvent.click(screen.getByRole("button", { name: "Editar 1.1.01.001 Cuenta de movimiento con nombre extenso para validar recorte 001" }));
    }

    const page = await browser.newPage({ viewport: { width, height: 800 } });
    try {
      await page.emulateMedia({ reducedMotion: "reduce" });
      await page.setContent(`<style>${css}</style>${container.innerHTML}`);
      const captureDir = process.env.COA_CAPTURE_DIR;
      if (captureDir) {
        mkdirSync(captureDir, { recursive: true });
        await page.screenshot({ path: resolve(captureDir, `${scenario}-${width}.png`),
          fullPage: scenario !== "modal", animations: "disabled" });
      }
      const g = await page.evaluate(() => {
        const inside = (el: Element) => {
          const r = el.getBoundingClientRect();
          return r.left >= -0.5 && r.right <= window.innerWidth + 0.5;
        };
        const scroll = document.querySelector(".acc-coa-list-card .table-scroll") as HTMLElement | null;
        const rows = Array.from(document.querySelectorAll("tbody tr")).map((r) => r.getBoundingClientRect().height);
        const firstRow = document.querySelector("tbody tr")?.getBoundingClientRect();
        const modal = document.querySelector(".zh-modal")?.getBoundingClientRect();
        const body = document.querySelector(".zh-modal-body");
        return {
          pageOverflow: document.documentElement.scrollWidth > window.innerWidth,
          controlsInside: Array.from(document.querySelectorAll(
            ".pg-kpi, .zh-filterbar__field, .acc-coa-tree-actions button, .pg-header button")).every(inside),
          tableScrollInside: scroll ? inside(scroll) : false,
          tableOverflowsBox: scroll ? scroll.scrollWidth > scroll.clientWidth + 1 : false,
          maxRowHeight: Math.max(...rows),
          firstRowTop: firstRow?.top ?? 0,
          modal: modal && { top: modal.top, bottom: modal.bottom, left: modal.left, right: modal.right },
          modalBodyFits: !body || body.scrollWidth <= body.clientWidth + 1,
        };
      });
      expect(g.pageOverflow).toBe(false);
      expect(g.controlsInside).toBe(true);
      expect(g.tableScrollInside).toBe(true);
      // Tablet y escritorio: las cinco columnas caben sin desplazamiento interno.
      if (width >= 1440) expect(g.tableOverflowsBox).toBe(false);
      // Fila compacta (table--compact): en escritorio, una sola línea por cuenta.
      if (width >= 1440) expect(g.maxRowHeight).toBeLessThan(60);
      // Mejor uso vertical: en escritorio la primera cuenta aparece en el primer pliegue.
      if (width >= 1440 && scenario !== "modal") expect(g.firstRowTop).toBeLessThan(560);
      if (scenario === "modal") {
        expect(g.modal).toBeTruthy();
        expect(g.modal!.top).toBeGreaterThanOrEqual(0);
        expect(g.modal!.bottom).toBeLessThanOrEqual(800);
        expect(g.modal!.left).toBeGreaterThanOrEqual(0);
        expect(g.modal!.right).toBeLessThanOrEqual(width);
        expect(g.modalBodyFits).toBe(true);
      }
    } finally {
      await page.close();
    }
  }, 30_000);
}
