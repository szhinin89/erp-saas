import { test as base, expect, type Page } from "@playwright/test";
import { createServer } from "vite";

// Nueva compra (rediseño visual): página REAL + CSS real + LayoutFrame real, API simulada.
// Verifica sin overflow horizontal del documento ni de las líneas a 1440/1024/768/390 px y deja
// capturas en test-results/purchases-form-*.png para la comparación con la referencia aprobada.
const test = base.extend<object, { server: string }>({
  server: [async ({}, use) => {
    const server = await createServer({ server: { host: "127.0.0.1", port: 4194, strictPort: true } });
    try { await server.listen(); await use("http://127.0.0.1:4194"); } finally { await server.close(); }
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
await import('/e2e/fixtures/purchases-form.tsx');</script></body></html>`;

const WAREHOUSES = [{ id: "wh-1", code: "B01", name: "Bodega principal", isActive: true }];
const line = (i: number, linked: boolean) => ({
  id: `l${i}`, itemId: linked ? "item-1" : null, description: linked ? "COLA-500 — Cola 500 ml" : "",
  snapshotSku: linked ? "COLA-500" : null, snapshotItemName: linked ? "Cola 500 ml" : null, snapshotSupplierCode: "13563",
  packagingLevelId: null, uomCode: "UNIT", baseUomCode: "UNIT", conversionFactor: 1, quantityInBaseUom: 12 + i,
  quantity: 12 + i, unitPrice: 0.5, discountPct: 0, discountAmount: 0, freightAllocated: 0, otherCostsAllocated: 0,
  totalLineCost: 6, landedUnitCost: 0.5, taxableBase: 6, vatCode: "4", vatRate: 15, vatAmount: 0.9, snapshotVatName: "IVA 15%",
  iceCode: null, iceRate: 0, iceAmount: 0, snapshotIceName: null, irbpnrCode: null, irbpnrRate: null, irbpnrAmount: 0,
  snapshotIrbpnrName: null, taxes: [], taxInclusiveTotal: 6.9, snapshotItemPvp: 0.85, warehouseId: "wh-1",
  snapshotWarehouseCode: "B01", purchaseOrderDetailId: null, orderedQuantity: null, purchaseReceptionLineId: null,
  notes: null, sortOrder: i,
});
const invoice = (status: string, lines: number) => ({
  id: "inv-1", supplierId: "sup-1", supplierName: "Distribuidora Andina S.A.", supplierTaxId: "1790012345001",
  docTypeCode: "01", invoiceNumber: "001-001-000000123", issueDate: "2026-11-10", accessKey: null, authorizationNumber: null,
  authorizationDate: null, taxSupportCode: "01", sriPaymentMethodCode: "01", sriPaymentMethodName: "Sin utilización del sistema financiero",
  currencyCode: "USD", exchangeRate: 1, purchaseOrderId: null, purchaseOrderNumber: null, globalWarehouseId: "wh-1",
  paymentTermId: "", paymentTermName: "", paymentTermInstallments: 1, paymentTermDaysBetween: 0, creditTermDays: 0, dueDate: null,
  notes: null, status, cancelReason: null, cancelledAt: null, cancelledBy: null, subtotal: 6 * lines, totalDiscount: 0, totalIce: 0,
  totalVat: 0.9 * lines, totalFreight: 0, totalOtherCosts: 0, grandTotal: 6.9 * lines, totalIrbpnr: 0,
  lines: Array.from({ length: lines }, (_, i) => line(i, true)), paymentSchedules: [], createdAt: "2026-11-10T10:00:00Z", updatedAt: null,
});

const ITEM_CONTEXT = {
  itemId: "item-1", sku: "COLA-500", shortName: "Cola 500 ml", description: "COLA-500 — Cola 500 ml", baseUomCode: "UNIT",
  participatesInInventory: true, supplierCode: "13563", packagingLevels: [
    { id: "unit", name: "UNIDAD", baseQuantity: 1, uomCode: "UNIT", isBaseUnit: true, isPurchaseDefault: true },
  ], currentStock: 48, availableStock: 40, reservedStock: 8, averageCost: 0.52, lastPurchaseCost: 0.5, pvp: 0.85, previousPrice: 0.8,
  maxDiscountPercent: 5, purchaseVatCode: "4", vatPercent: 15, exciseTaxCode: null, icePercent: 0, hasVat: true, hasIce: false,
  costMargin: 0.3, costMarginPercent: 35,
};
type Scenario = "new" | "draft" | "confirmed";
const unhandled = new Set<string>();

type Locale = "es" | "en";

async function open(page: Page, width: number, height: number, scenario: Scenario = "new", lines = 3, locale: Locale = "es") {
  await page.setViewportSize({ width, height });
  const json = (body: unknown) => ({ contentType: "application/json", body: JSON.stringify({ data: body }) });
  await page.route("**/__purchases*", (route) => route.fulfill({ contentType: "text/html", body: SHELL }));
  await page.route((u) => u.pathname.startsWith("/api/"), (route) => {
    const url = route.request().url();
    if (url.includes("/purchases/inv-1")) return route.fulfill(json(invoice(scenario === "confirmed" ? "Confirmed" : "Draft", lines)));
    if (url.includes("/purchases/items/context")) return route.fulfill(json(ITEM_CONTEXT));
    if (url.includes("sri-doc-types")) return route.fulfill(json([{ code: "01", name: "Factura", shortName: "FAC", isElectronic: true }]));
    if (url.includes("sri-vat-rates")) return route.fulfill(json([{ code: "4", name: "IVA 15%", percentage: 15 }, { code: "0", name: "IVA 0%", percentage: 0 }]));
    if (url.includes("sri-payment-methods")) return route.fulfill(json([{ code: "01", name: "Sin utilización del sistema financiero" }]));
    if (url.includes("sri-tax-support")) return route.fulfill(json([{ code: "01", name: "Crédito tributario IVA" }]));
    if (url.includes("warehouses")) return route.fulfill(json(WAREHOUSES));
    unhandled.add(url.replace(/^https?:\/\/[^/]+/, ""));
    return route.fulfill(json([]));
  });
  await page.goto(scenario === "new" ? "/__purchases" : "/__purchases?invoiceId=inv-1");
  await page.waitForFunction(() => typeof (window as unknown as { __setLocale?: unknown }).__setLocale === "function");
  if (locale === "en") await setLocale(page, "en");
  const loaded = await page.evaluate(async () => {
    try { await document.fonts.load('24px "Material Symbols Outlined"', "help"); } catch { /* sin red */ }
    return document.fonts.check('24px "Material Symbols Outlined"');
  });
  if (!loaded) await page.addStyleTag({ content: ".material-symbols-outlined{display:inline-block;width:1em;overflow:hidden;white-space:nowrap}" });
}

// Mismo `setLocale` del I18nProvider que usa el selector de idioma de la app: sin recargar la página.
async function setLocale(page: Page, locale: Locale) {
  await page.evaluate((l) => (window as unknown as { __setLocale: (x: string) => void }).__setLocale(l), locale);
  await page.waitForFunction((l) => (window as unknown as { __locale: string }).__locale === l, locale);
}

async function openNewTab(page: Page) {
  await expect(page.locator("#purchase-supplier")).toBeVisible();
}

// Desbordamiento: del documento, de las tarjetas de línea y de cualquier elemento del formulario cuyo
// borde derecho quede fuera del viewport (también cuando un contenedor lo recorta).
const overflow = (page: Page) => page.evaluate(() => {
  const root = document.documentElement;
  const offenders = [...document.querySelectorAll<HTMLElement>(".pdl-line-card, .pdl-line, .pdl-line__summary, .pf-header-cards-row--purchase")]
    .filter((el) => el.scrollWidth - el.clientWidth > 1).map((el) => el.className);
  const outside = [...document.querySelectorAll<HTMLElement>(".pf-purchase-form *")]
    .filter((el) => el.getBoundingClientRect().width > 0 && el.getBoundingClientRect().right > window.innerWidth + 1
      && !el.closest(".pdl-search__dropdown")
      // Scroll contenido por diseño (cronograma): el contenedor `.table-scroll` sí debe caber.
      && !(el.closest(".table-scroll, #purchase-retention .pf-card__body") && !el.matches(".table-scroll, #purchase-retention .pf-card__body")))
    .slice(0, 5).map((el) => `${el.tagName}.${String(el.className).slice(0, 40)}`);
  // Contención: nada del detalle de productos debe salir de su tarjeta (el menú de resultados es un popover).
  const card = document.querySelector<HTMLElement>(".pdl-container")?.getBoundingClientRect();
  const escaped = card ? [...document.querySelectorAll<HTMLElement>(".pdl-container *")]
    .filter((el) => el.getBoundingClientRect().width > 0 && el.getBoundingClientRect().right > card.right + 1
      && !el.closest(".pdl-search__dropdown, .table-scroll"))
    .slice(0, 5).map((el) => `${el.tagName}.${String(el.className).slice(0, 40)}`) : [];
  return { doc: root.scrollWidth - root.clientWidth, offenders, outside, escaped };
});
const expectNoOverflow = async (page: Page) => {
  const o = await overflow(page);
  expect(o.doc, JSON.stringify(o)).toBeLessThanOrEqual(0);
  expect(o.offenders, JSON.stringify(o)).toEqual([]);
  expect(o.outside, JSON.stringify(o)).toEqual([]);
  expect(o.escaped, JSON.stringify(o)).toEqual([]);
};

const SIZES = [[1440, 900], [1024, 800], [768, 900], [412, 915], [390, 844], [360, 740]] as const;

const ADD = /Agregar producto|Add product/;
const QTY = /Cantidad a registrar|Quantity to record/;

for (const [width, height] of SIZES) {
  for (const locale of ["es", "en"] as const) {
    test(`${width} px [${locale}]: nueva compra vacía y con líneas con incidencias sin overflow`, async ({ page }) => {
      await open(page, width, height, "new", 3, locale);
      await openNewTab(page);
      await page.screenshot({ path: `test-results/purchases-form-${locale}-${width}-empty.png`, fullPage: true });
      await page.getByRole("button", { name: ADD }).first().click();
      await page.getByRole("button", { name: ADD }).first().click();
      await page.screenshot({ path: `test-results/purchases-form-${locale}-${width}-lines.png`, fullPage: true });
      await expectNoOverflow(page);
    });

    test(`${width} px [${locale}]: borrador con productos vinculados`, async ({ page }) => {
      await open(page, width, height, "draft", 2, locale);
      await expect(page.locator("#purchase-lines .pdl-line-card").first()).toBeVisible();
      await page.screenshot({ path: `test-results/purchases-form-${locale}-${width}-draft.png`, fullPage: true });
      await expectNoOverflow(page);
    });

    test(`${width} px [${locale}]: compra confirmada en modo lectura`, async ({ page }) => {
      await open(page, width, height, "confirmed", 2, locale);
      await expect(page.locator("#purchase-lines .pdl-line-card").first()).toBeVisible();
      await page.screenshot({ path: `test-results/purchases-form-${locale}-${width}-readonly.png`, fullPage: true });
      await expectNoOverflow(page);
      // Modo lectura: los controles de edición de la línea siguen deshabilitados.
      await expect(page.getByRole("button", { name: ADD })).toBeDisabled();
      await expect(page.getByLabel(QTY).first()).toBeDisabled();
    });
  }
}

// Información electrónica y Observaciones: normalmente cerradas; aquí se abren solo para la captura.
for (const [width, height] of [[1440, 900], [390, 844]] as const) {
  for (const locale of ["es", "en"] as const) {
    test(`${width} px [${locale}]: Información electrónica y Observaciones abiertas sin overflow`, async ({ page }) => {
      await open(page, width, height, "new", 3, locale);
      await page.getByRole("button", { name: /Información electrónica|Electronic information/ }).click();
      await page.getByRole("button", { name: /Observaciones|Notes/ }).click();
      await expect(page.locator("#purchase-electronic-panel")).toBeVisible();
      await expect(page.locator("#purchase-notes-panel")).toBeVisible();
      await page.screenshot({ path: `test-results/purchases-form-${locale}-${width}-extras-open.png`, fullPage: true });
      await expectNoOverflow(page);
    });
  }
}

// Cambio de idioma en caliente: sin recargar, la pantalla pasa de español a inglés y vuelve.
test("cambio de idioma es -> en -> es sin recargar la página", async ({ page }) => {
  await open(page, 1440, 900, "new", 3, "es");
  await page.evaluate(() => { (window as unknown as { __marker: number }).__marker = 42; });
  await page.getByRole("button", { name: ADD }).first().click();
  await page.getByRole("button", { name: ADD }).first().click();
  await expect(page.getByRole("heading", { name: "Nueva compra" })).toBeVisible();
  await expect(page.getByText("Datos del proveedor")).toBeVisible();
  await expect(page.getByRole("button", { name: "Guardar borrador" })).toBeVisible();
  await expect(page.getByText("La descripción es obligatoria.").first()).toBeVisible();

  await setLocale(page, "en");
  await expect(page.getByRole("heading", { name: "New purchase" })).toBeVisible();
  await expect(page.getByText("Supplier data")).toBeVisible();
  await expect(page.getByRole("button", { name: "Save draft" })).toBeVisible();
  await expect(page.getByRole("button", { name: ADD }).first()).toBeVisible();
  await expect(page.getByText("No product linked").first()).toBeVisible();
  await expect(page.getByText("Product detail")).toBeVisible();
  await expect(page.getByText("Payment schedule")).toBeVisible();
  // Sin recarga: el marcador de la página sigue ahí.
  expect(await page.evaluate(() => (window as unknown as { __marker: number }).__marker)).toBe(42);
  // Los mensajes de validación ya mostrados: se registra si siguen en español hasta revalidar.
  const stale = await page.getByText("La descripción es obligatoria.").count();
  console.log("Mensaje de validación de descripción tras cambiar a EN — instancias en español:", stale,
    "| en inglés:", await page.getByText("The description is required.").count());

  await setLocale(page, "es");
  await expect(page.getByRole("heading", { name: "Nueva compra" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Guardar borrador" })).toBeVisible();
  expect(await page.evaluate(() => (window as unknown as { __marker: number }).__marker)).toBe(42);
});

// Ayuda contextual (?) del proveedor y de la bodega: texto en el idioma activo.
for (const [locale, expected] of [["es", /Selecciónelo por RUC o nombre/], ["en", /Select it by tax ID or name/]] as const) {
  test(`ayuda contextual del proveedor [${locale}]`, async ({ page }) => {
    await open(page, 1440, 900, "new", 3, locale);
    await page.locator("#purchase-supplier h4 button").first().click();
    await expect(page.getByText(expected)).toBeVisible();
  });
}

// Teclado: dentro de la barra del detalle de productos el orden de Tab (orden natural del DOM, sin
// tabindex positivo) debe coincidir con el orden de lectura visual (fila a fila, de izquierda a
// derecha) y cada control enfocado debe mostrar un indicador de foco visible.
for (const [width, height] of [[1440, 900], [390, 844]] as const) {
  for (const scenario of ["new", "draft"] as const) {
    test(`teclado ${width} px (${scenario === "new" ? "Ver motivo + Distribuir deshabilitado" : "Distribuir habilitado"}): Tab sigue el orden visual y el foco es visible`, async ({ page }) => {
      await open(page, width, height, scenario, 2);
      await expect(page.locator(".pdl-topbar")).toBeVisible();
      if (scenario === "new") await page.getByRole("button", { name: /Agregar/ }).first().click();
      // Punto de partida: el último control antes de la barra (Observaciones).
      await page.getByRole("button", { name: /Observaciones/ }).focus();
      const stops: { name: string; top: number; left: number; focusVisible: boolean }[] = [];
      for (let i = 0; i < 12; i++) {
        await page.keyboard.press("Tab");
        const stop = await page.evaluate(() => {
          const el = document.activeElement as HTMLElement | null;
          if (!el || !el.closest(".pdl-topbar")) return null;
          const r = el.getBoundingClientRect();
          const cs = getComputedStyle(el);
          const outlined = cs.outlineStyle !== "none" && parseFloat(cs.outlineWidth) > 0;
          const shadowed = cs.boxShadow !== "none";
          return {
            name: el.getAttribute("aria-label") || el.textContent?.trim() || el.tagName,
            top: r.top + window.scrollY, left: r.left, focusVisible: outlined || shadowed,
          };
        });
        if (!stop) break;
        stops.push(stop);
      }
      console.log(`Tab ${width}/${scenario}:`, stops.map((x) => `${x.name} (${Math.round(x.left)},${Math.round(x.top)})`).join(" | "));
      expect(stops.length).toBeGreaterThanOrEqual(4);
      for (let k = 1; k < stops.length; k++) {
        const a = stops[k - 1];
        const b = stops[k];
        const nextRow = b.top > a.top + 20;
        const sameRow = Math.abs(b.top - a.top) <= 20;
        expect(nextRow || (sameRow && b.left >= a.left), `${a.name} -> ${b.name} rompe el orden visual`).toBe(true);
      }
      expect(stops.every((x) => x.focusVisible), `foco visible: ${JSON.stringify(stops.map((x) => [x.name, x.focusVisible]))}`).toBe(true);
      if (scenario === "new") expect(stops[0].name).toMatch(/Ver motivo/);
      else expect(stops[0].name).toMatch(/Distribuir/);
      expect(stops[stops.length - 1].name).toMatch(/Agregar/);
    });
  }
}

test("API sin mock registrada", () => {
  console.log("API sin mock:", [...unhandled].join(", "));
});
