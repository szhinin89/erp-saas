import { test, expect, type Page } from "@playwright/test";
const pageErrors = new WeakMap<Page, string[]>();
async function inside(page: Page) {
  await expect.poll(() => page.locator(".zh-help-popover").evaluate(el => {
    const r = el.getBoundingClientRect();
    return r.left >= 11.5 && r.top >= 11.5 && r.right <= document.documentElement.clientWidth - 11.5 && r.bottom <= document.documentElement.clientHeight - 11.5 && document.documentElement.scrollWidth <= document.documentElement.clientWidth;
  })).toBe(true);
}
test.beforeEach(async ({ page }) => {
  const errors: string[] = [];
  pageErrors.set(page, errors);
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
  await page.route("**/__help-test", route => route.fulfill({ contentType: "text/html", body: `<div id="root"></div><script type="module">
    import RefreshRuntime from '/@react-refresh';
    RefreshRuntime.injectIntoGlobalHook(window);
    window.$RefreshReg$ = () => {};
    window.$RefreshSig$ = () => type => type;
    window.__vite_plugin_react_preamble_installed__ = true;
    await import('/e2e/fixtures/help-popover.tsx');
  </script>` }));
  await page.goto("/__help-test");
});
test.afterEach(async ({ page }) => {
  await page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  expect(pageErrors.get(page)).toEqual([]);
});
test("real geometry: edges, responsive, tall content, resize, scroll and observations", async ({ page }) => {
  await page.setViewportSize({ width: 800, height: 600 });
  await page.locator("#anchor").click(); await inside(page);
  for (const [left, top] of [[780, 200], [0, 200], [400, 570]]) {
    await page.locator("#anchor").evaluate((el, pos) => { el.style.left = `${pos[0]}px`; el.style.top = `${pos[1]}px`; el.style.width = `${20 + pos[0] / 100}px`; }, [left, top]);
    await inside(page);
  }
  await page.setViewportSize({ width: 240, height: 300 }); await inside(page);
  await page.locator("#content").evaluate(el => { el.textContent = "Long content ".repeat(500); }); await inside(page);
  await page.locator("#ancestor").evaluate(el => { el.style.height = "2000px"; });
  await page.evaluate(() => window.scrollTo(0, 100)); await inside(page);
  await expect(page.locator(".zh-help-popover")).toHaveCSS("overflow-y", "auto");
  await expect(page.locator(".zh-help-popover")).toHaveCSS("z-index", "1200");
  expect(await page.locator(".zh-help-popover").evaluate(el => el.parentElement === document.body)).toBe(true);
});
test("section and field consumers preserve keyboard, pin, Escape and outside clicks", async ({ page }) => {
  const buttons = page.locator("#consumers button");
  for (let index = 0; index < 4; index++) {
    const button = buttons.nth(index);
    await button.focus(); await inside(page);
    await button.press("Enter");
    await page.mouse.move(5, 5); await inside(page);
    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog")).toHaveCount(0); await expect(button).toBeFocused();
    await button.click(); await inside(page);
    await page.mouse.click(5, 5); await expect(page.getByRole("dialog")).toHaveCount(0);
  }
});
test("preserves official header, dropdown and modal stacking", async ({ page }) => {
  await page.locator("#anchor").click(); await inside(page);
  for (const className of ["zh-app-tenantHeader", "zh-picker__dropdown", "zh-modal-overlay"]) {
    const result = await page.evaluate(className => {
      const panel = document.querySelector<HTMLElement>(".zh-help-popover")!;
      const layer = document.createElement("div");
      layer.className = className === "zh-app-tenantHeader" ? `zh-tenant-header ${className}` : className;
      Object.assign(layer.style, { position: "fixed", inset: "0", width: "100vw", height: "100vh", maxHeight: "none", margin: "0" });
      document.body.append(layer);
      const r = panel.getBoundingClientRect();
      const hit = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
      const result = { layerZ: getComputedStyle(layer).zIndex, helpOnTop: panel.contains(hit) };
      layer.remove(); return result;
    }, className);
    expect(result.helpOnTop).toBe(true);
    expect(result.layerZ).toBe(className === "zh-modal-overlay" ? "1000" : className === "zh-picker__dropdown" ? "50" : "40");
  }
});
