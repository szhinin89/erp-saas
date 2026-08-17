import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// Audita SalesPage.css directamente (no jsdom): confirma que las filas
// complementarias del método de pago (monto/referencia/crédito) ya no dependen
// de --color-on-primary / color-mix pensado para el antiguo fondo primario
// activo (SALES-DS-TOGGLE-TILE-10A). El tile en sí (ZHToggleTile) sí sigue
// usando --color-on-primary legítimamente cuando está activo — eso vive en
// zh-ui.css y está fuera de este audit.
const css = readFileSync(new URL("./SalesPage.css", import.meta.url), "utf8");

function ruleBlockFor(selector: string): string {
  const start = css.indexOf(`${selector} {`);
  expect(start, `selector "${selector}" no encontrado en SalesPage.css`).toBeGreaterThan(-1);
  const end = css.indexOf("}", start);
  return css.slice(start, end);
}

describe("SalesPage.css — contraste de filas complementarias de método de pago", () => {
  it.each([
    ".sales-payment-dollar",
    ".sales-payment-input",
    ".sales-payment-ref-amount",
    ".sales-payment-ref-count",
    ".sales-payment-credit-amount",
  ])("%s no usa color-mix con --color-on-primary", (selector) => {
    const block = ruleBlockFor(selector);
    expect(block).not.toMatch(/color-mix/);
    expect(block).not.toMatch(/--color-on-primary/);
  });

  it("sales-payment-method ya no define sales-payment-method--active", () => {
    expect(css).not.toContain(".sales-payment-method--active");
  });

  it("sales-payment-method__btn ya no está definido", () => {
    expect(css).not.toContain(".sales-payment-method__btn");
  });
});
