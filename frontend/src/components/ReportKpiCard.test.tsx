// @vitest-environment jsdom
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { act, cleanup, render } from "@testing-library/react";
import { ReportKpiCard } from "./ReportPageTemplate";
import { ZHNumberValue } from "./zh/ZHNumberValue";
import { ZHMoneyValue } from "./zh/ZHMoneyValue";
import { setPrecisionPolicyForTests } from "../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../test/precisionPolicyFixture";

// ZH-DESIGN-SYSTEM-PRECISION-02C — ReportKpiCard.value acepta ReactNode (backward-compatible con
// string). La tarjeta es layout: no conoce precisión; la semántica la declara el valor hijo.

afterEach(() => cleanup());

const valueEl = (c: HTMLElement) => c.querySelector(".pg-kpi-value") as HTMLElement;

describe("ReportKpiCard — value (02C)", () => {
  it("string legacy: mismo DOM que antes (texto directo dentro de p.pg-kpi-value)", () => {
    const { container } = render(<ReportKpiCard label="Productos" value="12" />);
    const el = valueEl(container);
    expect(el.tagName).toBe("P");
    expect(el.className).toBe("pg-kpi-value");
    expect(el.childNodes).toHaveLength(1);
    expect(el.firstChild?.nodeType).toBe(Node.TEXT_NODE);
    expect(el.textContent).toBe("12");
  });

  it("string con unit y valueTone conserva clases y orden", () => {
    const { container } = render(
      <ReportKpiCard label="Stock" value="5" unit="und." valueTone="danger" />,
    );
    const el = valueEl(container);
    expect(el.className).toBe("pg-kpi-value pg-kpi-value--danger");
    expect(el.textContent).toBe("5und.");
    expect(el.lastElementChild?.className).toBe("pg-kpi-unit");
  });

  it("string vacío se renderiza vacío (contrato actual, sin placeholder)", () => {
    const { container } = render(<ReportKpiCard label="X" value="" />);
    expect(valueEl(container).textContent).toBe("");
  });

  it("ReactNode: ZHNumberValue se monta directamente dentro de p.pg-kpi-value, sin wrapper extra", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 3 });
    const { container } = render(
      <ReportKpiCard label="Unidades Totales" value={<ZHNumberValue value={12.5} precision="quantity" />} />,
    );
    const el = valueEl(container);
    expect(el.children).toHaveLength(1);
    expect(el.firstElementChild?.className).toBe(
      "zh-number-value zh-number-value--default zh-number-value--end",
    );
    expect(el.textContent).toBe("12.500");
  });

  it("ReactNode conserva la semántica: re-renderiza al cambiar la policy", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 3 });
    const { container } = render(
      <ReportKpiCard label="Unidades" value={<ZHNumberValue value={12.5} precision="quantity" />} />,
    );
    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 1 }));
    expect(valueEl(container).textContent).toBe("12.5");
  });

  it("ReactNode monetario y null (—) funcionan dentro del card", () => {
    const { container } = render(
      <ReportKpiCard label="Valor" value={<ZHMoneyValue value={null} precision="money" />} />,
    );
    expect(valueEl(container).textContent).toBe("—");
  });

  it("CSS: el valor DS anidado hereda tipografía y tono de .pg-kpi-value", () => {
    const css = readFileSync(resolve(__dirname, "../styles/page-template.css"), "utf-8").replace(/\r\n/g, "\n");
    const rule = css.match(/\.pg-kpi-value \.zh-number-value,\n\.pg-kpi-value \.zh-money-value \{([^}]*)\}/)?.[1] ?? "";
    for (const prop of ["font-size", "font-weight", "letter-spacing", "color"]) {
      expect(rule).toContain(`${prop}: inherit`);
    }
  });
});
