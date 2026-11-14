import { test as base, expect, type Page } from "@playwright/test";
import { createServer } from "vite";

// EXPENSE-UI-02 — formulario de Gastos real (página + CSS + LayoutFrame reales; API simulada):
// sin overflow del documento, resumen de importes siempre visible mientras se edita, y cambio
// inmediato del total al editar cantidad. Anchos 1280 / 900 / 390 px.
const test = base.extend<object, { server: string }>({
  server: [async ({}, use) => {
    const server = await createServer({ server: { host: "127.0.0.1", port: 4193, strictPort: true } });
    try { await server.listen(); await use("http://127.0.0.1:4193"); } finally { await server.close(); }
  }, { scope: "worker" }],
  baseURL: async ({ server }, use) => { await use(server); },
});

const SHELL = `<!doctype html><html><head><meta name="viewport" content="width=device-width, initial-scale=1.0" />
<style>.fx-header{height:56px;background:#fff;border-bottom:1px solid #ddd}</style>
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet" />
<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200" rel="stylesheet" />
</head><body><div id="root"></div><script type="module">
import RefreshRuntime from '/@react-refresh'; RefreshRuntime.injectIntoGlobalHook(window);
window.$RefreshReg$ = () => {}; window.$RefreshSig$ = () => type => type; window.__vite_plugin_react_preamble_installed__ = true;
await import('/e2e/fixtures/expense-form.tsx');</script></body></html>`;

const SUBCATS = ["a", "b", "c"].map((k, i) => ({
  id: `sub-${k}`, companyId: "c", parentId: "cat-1", code: `5.1.${i + 1}`, name: `Subcategoría ${k.toUpperCase()}`,
  description: null, level: "Subcategory", accountingAccountId: `acc-${k}`, isActive: true,
  accountingAccountCode: `5.1.0${i + 1}`, accountingAccountName: `Gasto ${k}`, children: [],
}));
const TREE = [{
  id: "type-1", companyId: "c", parentId: null, code: "5", name: "Gastos operativos", description: null, level: "Type",
  accountingAccountId: null, isActive: true,
  children: [{ id: "cat-1", companyId: "c", parentId: "type-1", code: "5.1", name: "Servicios", description: null, level: "Category", accountingAccountId: null, isActive: true, children: SUBCATS }],
}];
const line = (i: number) => ({
  id: `l${i}`, expenseSubcategoryId: SUBCATS[i % 3].id, snapshotAccountingAccountId: null, snapshotAccountingAccountCode: null,
  snapshotAccountingAccountName: null, description: `Concepto de prueba ${i + 1}`, quantity: 2, unitAmount: 50, discountAmount: 0,
  vatCode: "4", vatRate: 15, vatAmount: 15, taxInclusiveTotal: 115, sortOrder: i, notes: i === 1 ? "Observación existente" : null,
});
const doc = (n: number) => ({
  id: "exp-1", companyId: "c", branchId: "b", supplierId: "sup-1", supplierName: "Proveedor de prueba S.A.", supplierTaxId: "1790012345001",
  issueDate: "2026-11-10", accountingDate: "2026-11-10", documentType: "01", documentNumber: "001-001-000000123",
  authorizationNumber: "1011202601179001234500110010010000001231234567818", authorizationDate: "2026-11-10T10:00:00Z",
  paymentTermId: "", paymentTermName: "", dueDate: null, subtotal: 100 * n, totalDiscount: 0, totalTax: 15 * n, grandTotal: 115 * n,
  notes: null, taxSupportCode: "01", status: "Draft", lines: Array.from({ length: n }, (_, i) => line(i)),
  cancelReason: null, cancelledAt: null, cancelledBy: null,
});

async function open(page: Page, width: number, height: number, lines: number, eligible = true, reception = false) {
  await page.setViewportSize({ width, height });
  const json = (body: unknown) => ({ contentType: "application/json", body: JSON.stringify({ data: body }) });
  await page.route("**/__expense*", (route) => route.fulfill({ contentType: "text/html", body: SHELL }));
  await page.route((u) => u.pathname.startsWith("/api/"), (route) => {
    const url = route.request().url();
    if (url.includes("create-expense-draft")) {
      return route.fulfill(json({
        receptionDocumentId: "r1", accessKey: "1".repeat(49), supplierId: "sup-1", supplierName: "Proveedor de prueba S.A.",
        supplierTaxId: "1790012345001", issueDate: "2026-11-10", documentType: "01", documentNumber: "001-001-000000123",
        authorizationNumber: "1".repeat(49), authorizationDate: null, subtotal: 100, vatAmount: 15, total: 115,
      }));
    }
    if (url.includes("/expenses/categories/tree")) return route.fulfill(json(TREE));
    if (url.includes("retention-eligibility")) {
      return route.fulfill(json({
        canRetainVat: eligible, canRetainIncome: false, isSupplierExempt: false, hasRetainableBase: true,
        missingRetentionCode: false, isSupplierRequiredToKeepAccounting: true, isEligible: eligible,
        candidates: eligible ? [{ taxType: "IVA", retentionCode: "721" }] : [],
        reasons: ["La empresa actual está configurada para retener IVA.", "El proveedor no está marcado como exento."],
      }));
    }
    if (url.includes("/expenses/documents/exp-1")) return route.fulfill(json(doc(lines)));
    if (url.includes("sri-doc-types")) return route.fulfill(json([{ code: "01", name: "Factura", shortName: "FAC", isElectronic: true }]));
    if (url.includes("sri-vat-rates")) return route.fulfill(json([{ code: "4", name: "IVA 15%", percentage: 15 }, { code: "0", name: "IVA 0%", percentage: 0 }]));
    if (url.includes("sri-tax-support-codes")) return route.fulfill(json([{ code: "01", name: "Crédito Tributario para declaración de IVA" }]));
    return route.fulfill(json([]));
  });
  await page.goto(reception ? "/__expense?reception=1" : "/__expense");
  await expect(page.locator(".exp-doc-line").first()).toBeVisible();
  const loaded = await page.evaluate(async () => {
    try { await document.fonts.load('24px "Material Symbols Outlined"', "help"); } catch { /* sin red */ }
    return document.fonts.check('24px "Material Symbols Outlined"');
  });
  if (!loaded) await page.addStyleTag({ content: ".material-symbols-outlined{display:inline-block;width:1em;overflow:hidden;white-space:nowrap}" });
}

// En la app real el frame crece con el contenido (`min-height: 100vh`, sin altura fija): quien
// se desplaza es la VENTANA, igual que en Compras (`.pf-totals` sticky). Se mide contra el viewport.
const metrics = (page: Page) => page.evaluate(() => {
  const side = document.querySelector(".exp-doc-form-side") as HTMLElement;
  const r = side.getBoundingClientRect();
  const root = document.documentElement;
  return {
    docOverflow: root.scrollWidth - root.clientWidth,
    scrollable: root.scrollHeight - window.innerHeight,
    sideVisible: r.top >= 0 && r.bottom <= window.innerHeight + 1,
    sideTop: Math.round(r.top), sideBottom: Math.round(window.innerHeight - r.bottom), sideHeight: Math.round(r.height),
    columns: getComputedStyle(document.querySelector(".exp-doc-form-layout") as HTMLElement).display === "grid" ? 2 : 1,
  };
});
const scrollTo = (page: Page, y: number | "end") => page.evaluate((v) => {
  window.scrollTo({ top: v === "end" ? document.documentElement.scrollHeight : v, behavior: "instant" }); // `html` usa scroll-behavior: smooth
}, y);
const grand = async (page: Page) => (await page.locator(".exp-doc-total-row--grand").innerText()).replace(/\s+/g, " ");

for (const [width, height, label] of [[1280, 800, "1280"], [900, 800, "900"], [390, 800, "390"]] as const) {
  for (const lines of [1, 4]) {
    test(`${label} px, ${lines} concepto(s): sin overflow y resumen visible al editar`, async ({ page }) => {
      await open(page, width, height, lines);
      const top = await metrics(page);
      expect(top.docOverflow).toBeLessThanOrEqual(0);
      await page.screenshot({ path: `test-results/expense-form-${label}-${lines}-top.png` });
      expect(top.sideVisible, `aside visible sin scroll (${JSON.stringify(top)})`).toBe(true);
      for (const y of [Math.round(top.scrollable / 2), "end"] as const) {
        await scrollTo(page, y);
        const m = await metrics(page);
        expect(m.sideVisible, `aside visible tras scroll ${y} (${JSON.stringify(m)})`).toBe(true);
      }
      await scrollTo(page, Math.round(top.scrollable / 2));
      await page.screenshot({ path: `test-results/expense-form-${label}-${lines}-mid.png` });
      // Editar cantidad del primer concepto: el total visible cambia de inmediato.
      const before = await grand(page);
      const qty = page.getByLabel(/^Cantidad/).first();
      await qty.scrollIntoViewIfNeeded();
      await qty.fill("10");
      await expect.poll(() => grand(page)).not.toBe(before);
      const m = await metrics(page);
      expect(m.sideVisible).toBe(true);
      expect(m.columns).toBe(width > 900 ? 2 : 1);
    });
  }
}

test("390 px: el desglose se despliega sin salirse del viewport", async ({ page }) => {
  await open(page, 390, 800, 2);
  await scrollTo(page, "end");
  const collapsed = await metrics(page);
  await page.getByRole("button", { name: "Ver desglose" }).click();
  const expanded = await metrics(page);
  expect(expanded.sideVisible).toBe(true);
  expect(expanded.sideHeight).toBeGreaterThan(collapsed.sideHeight);
  await expect(page.getByRole("button", { name: /Guardar borrador/ })).toBeVisible();
  await page.screenshot({ path: "test-results/expense-form-390-breakdown.png" });
});

test("retención que no aplica: explicación y motivos visibles", async ({ page }) => {
  await open(page, 1280, 900, 1, false);
  await expect(page.getByText("Este gasto no lleva retención con la configuración actual.")).toBeVisible();
  await expect(page.getByText("La empresa actual está configurada para retener IVA.")).toBeVisible();
  await page.screenshot({ path: "test-results/expense-form-retention-no.png" });
});

test("retención que aplica: motivos plegados y alternables; checkbox sin estirar", async ({ page }) => {
  await open(page, 1280, 900, 1, true);
  await expect(page.getByText("Se puede generar retención")).toBeVisible();
  await expect(page.getByText("La empresa actual está configurada para retener IVA.")).toHaveCount(0);
  await page.getByRole("button", { name: /Ver motivos/ }).click();
  await expect(page.getByText("La empresa actual está configurada para retener IVA.")).toBeVisible();
  const box = await page.getByLabel("Aplicar retención a este gasto").boundingBox();
  expect(box!.width).toBeLessThan(30);
  await page.screenshot({ path: "test-results/expense-form-retention-yes.png" });
});

// Gasto nuevo desde XML con diferencia de importes (borrador vacío frente a un XML de 115.00).
const TOAST = "El total no coincide con el XML.";
for (const [width, label] of [[1280, "1280"], [900, "900"], [390, "390"]] as const) {
  test(`${label} px: XML con diferencia → indicador al abrir, toast solo tras ingresar importes, sin tapar el resumen`, async ({ page }) => {
    await open(page, width, 800, 1, true, true);
    // Al abrir (conceptos sin completar): indicador compacto visible y NINGÚN toast.
    const badge = page.getByRole("button", { name: "Total distinto al XML" });
    await expect(badge).toBeVisible();
    await page.waitForTimeout(400);
    await expect(page.getByRole("alert")).toHaveCount(0);
    expect((await metrics(page)).docOverflow).toBeLessThanOrEqual(0);

    // Interacción relevante: ingresar importes que no coinciden → un toast, sin bloquear la edición.
    await page.getByLabel(/^Cantidad/).first().fill("3");
    await page.getByLabel(/^Valor unitario/).first().fill("10");
    const toast = page.getByRole("alert").filter({ hasText: TOAST });
    await expect(toast).toHaveCount(1);
    await page.getByLabel(/^Valor unitario/).first().fill("12");
    await page.getByLabel(/^Cantidad/).first().fill("4");
    await expect(page.getByRole("alert").filter({ hasText: TOAST })).toHaveCount(1); // sin repeticiones
    await expect(badge).toBeVisible(); // el indicador persiste

    // Toast de una línea y sin solaparse con el resumen, tampoco con la página desplazada.
    await page.evaluate(() => window.scrollTo({ top: 300, behavior: "instant" }));
    const geo = await page.evaluate(() => {
      const t = document.querySelector(".zh-toast")!.getBoundingClientRect();
      const s = document.querySelector(".exp-doc-form-side")!.getBoundingClientRect();
      return {
        toastHeight: Math.round(t.height), toastBottom: Math.round(t.bottom), sideTop: Math.round(s.top),
        overlap: !(t.right <= s.left || t.left >= s.right || t.bottom <= s.top || t.top >= s.bottom),
      };
    });
    expect(geo.overlap, JSON.stringify(geo)).toBe(false);
    expect(geo.toastHeight).toBeLessThan(70); // una línea
    if (width > 900) {
      // Offset sticky ajustado al toast: separación mínima, sin hueco excesivo sobre el resumen.
      expect(geo.sideTop).toBeGreaterThanOrEqual(geo.toastBottom);
      expect(geo.sideTop - geo.toastBottom).toBeLessThanOrEqual(24);
      test.info().annotations.push({ type: "sticky-offset", description: JSON.stringify(geo) });
    }

    // Detalle accesible (Enter), dentro del viewport; Escape lo cierra y devuelve el foco.
    await badge.focus();
    await page.keyboard.press("Enter");
    const dialog = page.getByRole("dialog");
    await expect(dialog).toContainText("Total XML recibido: 115.00");
    await expect(dialog).toContainText("IVA XML recibido: 15.00");
    await expect(dialog).toContainText("Total calculado en pantalla: 55.20");
    const box = (await dialog.boundingBox())!;
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.y).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(width + 1);
    expect(box.y + box.height).toBeLessThanOrEqual(800 + 1);
    await page.screenshot({ path: `test-results/expense-form-mismatch-${label}.png` });
    await page.keyboard.press("Escape");
    await expect(dialog).toHaveCount(0);
    await expect(badge).toBeFocused();
    await expect(page.getByText(/Comprobante recibido/)).toHaveCount(0);
  });
}

// Alineación: los controles de una misma fila de la grilla comparten posición y altura.
for (const [width, label] of [[1280, "1280"], [900, "900"], [390, "390"]] as const) {
  test(`${label} px: campos y botones alineados por fila`, async ({ page }) => {
    await open(page, width, 800, 2);
    const rows = await page.evaluate(() => {
      const controls = Array.from(document.querySelectorAll(
        ".exp-doc-group .zh-field-control > :is(input, select, div:not(.exp-doc-field-with-help)), .exp-doc-field-with-help select, .exp-doc-line__grid .zh-field-control > :is(input, select)",
      )) as HTMLElement[];
      const byTop = new Map<string, number[]>();
      for (const el of controls) {
        const r = el.getBoundingClientRect();
        if (r.width === 0) continue;
        const key = `${Math.round(r.top + window.scrollY)}`;
        byTop.set(key, [...(byTop.get(key) ?? []), Math.round(r.height)]);
      }
      return [...byTop.entries()].filter(([, h]) => h.length > 1).map(([top, h]) => ({ top, heights: h }));
    });
    // Misma fila → misma altura (supplier picker es de otra altura y vive en su propia celda).
    // Tolerancia de 1px: input y select compactos del DS difieren 34/35px por sí mismos.
    for (const row of rows) {
      const inputs = row.heights.filter((h) => h < 60);
      expect(Math.max(...inputs) - Math.min(...inputs), `fila @${row.top}: ${row.heights}`).toBeLessThanOrEqual(1);
    }
    const buttons = await page.evaluate(() => Array.from(document.querySelectorAll(".exp-doc-form-side .zh-actions .zh-btn"))
      .map((b) => { const r = b.getBoundingClientRect(); return { top: Math.round(r.top), h: Math.round(r.height) }; }));
    expect(buttons.length).toBe(2);
    expect(buttons[0].top).toBe(buttons[1].top);
    expect(buttons[0].h).toBe(buttons[1].h);
  });
}
