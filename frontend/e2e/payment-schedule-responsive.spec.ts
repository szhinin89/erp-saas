import { test as base, expect, type Page } from "@playwright/test";
import { createServer } from "vite";

// UIUX Paquete D — cronograma de cuotas de Compras: sin overflow del documento, scroll contenido en la
// tabla y sin superposición de cabecera/pie, a 375 px y 1280 px. Monta el PaymentScheduleSection real.
const test = base.extend<object, { scheduleServer: string }>({
  scheduleServer: [async ({}, use) => {
    const server = await createServer({ server: { host: "127.0.0.1", port: 4192, strictPort: true } });
    try { await server.listen(); await use("http://127.0.0.1:4192"); } finally { await server.close(); }
  }, { scope: "worker" }],
  baseURL: async ({ scheduleServer }, use) => { await use(scheduleServer); },
});

// Mismo shell que index.html: la fuente de iconos llega por el <link> de Google Fonts.
const SHELL = `<!doctype html><html><head><meta name="viewport" content="width=device-width, initial-scale=1.0" />
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet" />
<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200" rel="stylesheet" />
</head><body><div id="root"></div><script type="module">
import RefreshRuntime from '/@react-refresh'; RefreshRuntime.injectIntoGlobalHook(window);
window.$RefreshReg$ = () => {}; window.$RefreshSig$ = () => type => type; window.__vite_plugin_react_preamble_installed__ = true;
await import('/e2e/fixtures/payment-schedule.tsx');</script></body></html>`;

async function open(page: Page, width: number, query = "") {
  await page.setViewportSize({ width, height: 800 });
  await page.route("**/__schedule*", (route) => route.fulfill({ contentType: "text/html", body: SHELL }));
  await page.goto(`/__schedule${query}`);
  await expect(page.locator("#purchase-schedule table")).toBeVisible();
  // Iconos: la fuente real si hay red; sin red, el glifo-ligadura ocupa 1em (misma métrica que la fuente).
  const loaded = await page.evaluate(async () => {
    try { await document.fonts.load('24px "Material Symbols Outlined"', "calendar_month"); } catch { /* sin red */ }
    return document.fonts.check('24px "Material Symbols Outlined"');
  });
  if (!loaded) {
    await page.addStyleTag({ content: ".material-symbols-outlined{display:inline-block;width:1em;overflow:hidden;white-space:nowrap}" });
  }
  return loaded;
}

const geometry = (page: Page) => page.evaluate(() => {
  const card = document.querySelector("#purchase-schedule") as HTMLElement;
  const cardRect = card.getBoundingClientRect();
  const scroll = document.querySelector("#purchase-schedule .table-scroll") as HTMLElement | null;
  const inside = (el: Element) => {
    const r = el.getBoundingClientRect();
    return r.left >= cardRect.left - 0.5 && r.right <= cardRect.right + 0.5;
  };
  const overlaps = (els: Element[]) => els.some((a, i) => els.slice(i + 1).some((b) => {
    const x = a.getBoundingClientRect();
    const y = b.getBoundingClientRect();
    return x.left < y.right - 0.5 && y.left < x.right - 0.5 && x.top < y.bottom - 0.5 && y.top < x.bottom - 0.5;
  }));
  const header = card.querySelector(".pf-card__header") as HTMLElement;
  const footer = card.querySelector(".pf-schedule-footer") as HTMLElement | null;
  return {
    docOverflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    scrollExists: !!scroll,
    scrollOverflow: scroll ? scroll.scrollWidth - scroll.clientWidth : -1,
    scrollInsideCard: scroll ? inside(scroll) : false,
    headerChildrenInside: [...header.children].every(inside),
    headerOverlap: overlaps([...header.children]),
    footerChildrenInside: footer ? [...footer.children].every(inside) : true,
    footerOverlap: footer ? overlaps([...footer.children]) : false,
  };
});

test("móvil 375 px: sin overflow del documento, scroll contenido en la tabla, sin superposición", async ({ page }) => {
  const realFont = await open(page, 375);
  const g = await geometry(page);
  test.info().annotations.push({ type: "icon-font", description: realFont ? "Material Symbols real" : "emulada (sin red)" });
  expect(g.docOverflow).toBeLessThanOrEqual(0);
  expect(g.scrollExists).toBe(true);
  expect(g.scrollInsideCard).toBe(true);
  expect(g.scrollOverflow).toBeGreaterThan(0); // la tabla desplaza dentro de su contenedor
  expect(g.headerChildrenInside).toBe(true);
  expect(g.headerOverlap).toBe(false);
  expect(g.footerChildrenInside).toBe(true);
  expect(g.footerOverlap).toBe(false);
  await page.screenshot({ path: "test-results/payment-schedule-375.png", fullPage: true });
});

test("móvil 375 px: tabla de solo lectura (cronograma persistido) contenida", async ({ page }) => {
  await open(page, 375, "?persisted=1");
  const g = await geometry(page);
  expect(g.docOverflow).toBeLessThanOrEqual(0);
  expect(g.scrollExists).toBe(true);
  expect(g.scrollInsideCard).toBe(true);
  expect(g.headerChildrenInside).toBe(true);
});

test("escritorio 1280 px: sin overflow y sin scroll interno; importes intactos", async ({ page }) => {
  await open(page, 1280);
  const g = await geometry(page);
  expect(g.docOverflow).toBeLessThanOrEqual(0);
  expect(g.scrollOverflow).toBeLessThanOrEqual(0);
  expect(g.headerChildrenInside).toBe(true);
  expect(g.footerOverlap).toBe(false);
  const amounts = await page.locator("#purchase-schedule input.pf-schedule-input--amount")
    .evaluateAll((els) => els.map((e) => (e as HTMLInputElement).value));
  expect(amounts).toEqual(["333.34", "333.33", "1234567.89"]);
  await expect(page.locator(".pf-schedule-footer")).toContainText("1235234.56");
  await expect(page.locator(".pf-schedule-footer")).toContainText("1001234.56");
  await page.screenshot({ path: "test-results/payment-schedule-1280.png", fullPage: true });
});

for (const width of [375, 1280]) {
  test(`edición y foco a ${width} px`, async ({ page }) => {
    await open(page, width);
    const row = page.locator("#purchase-schedule tbody tr").nth(0);
    await row.locator("input").nth(0).fill("2026-12-20");
    const amount = row.locator("input").nth(1);
    await amount.click();
    await amount.fill("400.00");
    await amount.press("Tab");
    const notes = row.locator("input").nth(2);
    await notes.click();
    await notes.pressSequentially("ab");
    await expect(notes).toBeFocused();
    await expect(notes).toHaveValue("ab");
    const log = await page.evaluate(() => (window as unknown as { __scheduleLog: string[] }).__scheduleLog);
    expect(log).toEqual(["update:0:dueDate:2026-12-20", "update:0:amount:400", "update:0:notes:a", "update:0:notes:ab"]);
    await page.locator("#purchase-schedule tbody tr").nth(2).getByRole("button").click();
    await expect(page.locator("#purchase-schedule tbody tr")).toHaveCount(2);
    await expect(page.locator(".pf-schedule-footer")).toContainText("733.33"); // 400 + 333.33
  });
}
