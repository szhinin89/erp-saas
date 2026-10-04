import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// POS-VIEWPORT-LAYOUT-01 — auditoría de CSS (no jsdom): invariantes del layout de caja.
// La página de venta ocupa el viewport (opt-in del shell) y el ÚNICO scroll vertical es el del
// detalle de líneas, con la cabecera de columnas fija.
const invoiceCss = readFileSync(new URL("../styles/sales-invoice.css", import.meta.url), "utf8");
const productCss = readFileSync(new URL("../styles/sales-product-card.css", import.meta.url), "utf8");
const shellCss = readFileSync(
  new URL("../../../components/layout/LayoutFrame.css", import.meta.url),
  "utf8",
);

/** Bloque de reglas de un selector, sin comentarios (los comentarios citan el CSS antiguo). */
function block(css: string, selector: string): string {
  const start = css.indexOf(`${selector} {`);
  expect(start, `selector "${selector}" no encontrado`).toBeGreaterThan(-1);
  return css.slice(start, css.indexOf("}", start)).replace(/\/\*[\s\S]*?\*\//g, "");
}

describe("POS — layout de viewport fijo", () => {
  it(".sf-layout no usa la altura mágica calc(100vh - …) y se ajusta al contenedor (flex + min-height 0)", () => {
    const b = block(invoiceCss, ".sf-layout");
    expect(b).not.toMatch(/100vh/);
    expect(b).toMatch(/flex:\s*1/);
    expect(b).toMatch(/min-height:\s*0/);
    expect(b).toMatch(/grid-template-rows:\s*auto\s+minmax\(0,\s*1fr\)\s+auto/);
  });

  it(".sf-main no scrollea: el scroll vertical es exclusivo de .sf-products", () => {
    expect(block(invoiceCss, ".sf-main")).not.toMatch(/overflow-y:\s*auto/);
    expect(block(productCss, ".sf-products")).toMatch(/overflow-y:\s*auto/);
  });

  it("la cabecera de columnas es sticky dentro del detalle", () => {
    const b = block(productCss, ".sfl-header");
    expect(b).toMatch(/position:\s*sticky/);
  });

  it("Cliente y cobro no se comprimen ni tienen zonas de scroll separadas", () => {
    expect(block(invoiceCss, ".sf-sidebar > *")).toMatch(/flex-shrink:\s*0/);
    expect(invoiceCss).not.toContain(".sf-sidebar__context {");
    expect(invoiceCss).not.toContain(".sf-sidebar__checkout {");
    expect(block(invoiceCss, ".sf-layout")).toMatch(/overflow:\s*hidden/);
  });

  it("el shell expone el opt-in .shell-fill-viewport sin afectar a otras páginas", () => {
    expect(shellCss).toContain(".shell-content-frame--tenant:has(.shell-fill-viewport)");
    expect(block(shellCss, ".shell-content-frame--tenant")).toMatch(/min-height:\s*100vh/);
  });
});
