// @vitest-environment jsdom
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { act, cleanup, render } from "@testing-library/react";
import { ZHNumberValue } from "./ZHNumberValue";
import { ZHMoneyValue } from "./ZHMoneyValue";
import { ZHLocaleProvider } from "./ZHLocaleProvider";
import { formatDecimalDisplay, formatMoney } from "../../lib/sanitizers";
import {
  setPrecisionPolicyForTests,
  type PrecisionPolicy,
} from "../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../test/precisionPolicyFixture";

afterEach(() => {
  cleanup();
});

/** Escalas deliberadamente distintas para detectar semánticas cruzadas. */
const POLICY_A: PrecisionPolicy = {
  ...TEST_PRECISION_POLICY,
  quantityDecimals: 4,
  percentageDecimals: 2,
  conversionFactorDecimals: 6,
};
const POLICY_B: PrecisionPolicy = {
  ...POLICY_A,
  quantityDecimals: 1,
  percentageDecimals: 3,
  conversionFactorDecimals: 8,
};

const root = (c: HTMLElement) => c.firstElementChild as HTMLElement;
const amount = (c: HTMLElement) => c.querySelector(".zh-number-value__amount")?.textContent;

describe("ZHNumberValue — precisión semántica (02A)", () => {
  it.each([
    ["quantity", 12.5, "12.5000"],
    ["percentage", 12.5, "12.50"],
    ["conversionFactor", 1.2345678, "1.234568"],
  ] as const)("%s con policy A: %s → %s", (precision, value, expected) => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(<ZHNumberValue value={value} precision={precision} />);
    expect(amount(container)).toBe(expected);
  });

  it("conversionFactor sigue la policy B (8 decimales)", () => {
    setPrecisionPolicyForTests(POLICY_B);
    const { container } = render(<ZHNumberValue value={1.2345678} precision="conversionFactor" />);
    expect(amount(container)).toBe("1.23456780");
  });

  it("reactivo A → B sin remount (quantity 4 → 1)", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(<ZHNumberValue value={12.25} precision="quantity" />);
    const node = container.querySelector(".zh-number-value__amount");
    expect(node?.textContent).toBe("12.2500");
    act(() => setPrecisionPolicyForTests(POLICY_B));
    expect(container.querySelector(".zh-number-value__amount")).toBe(node);
    expect(node?.textContent).toBe("12.3");
  });

  it("decimals explícito gana sobre precision", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(<ZHNumberValue value={12.5} precision="quantity" decimals={0} />);
    expect(amount(container)).toBe("13");
  });

  it("solo decimals: no depende de la policy cargada", () => {
    setPrecisionPolicyForTests(null);
    const { container } = render(<ZHNumberValue value={2} decimals={3} />);
    expect(amount(container)).toBe("2.000");
  });
});

describe("ZHNumberValue — presentación (02A)", () => {
  it("null/undefined → '—' con --empty, sin prefijo ni sufijo", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(<ZHNumberValue value={null} precision="percentage" suffix="%" />);
    expect(root(container).textContent).toBe("—");
    expect(root(container).className).toContain("zh-number-value--empty");
    cleanup();
    const u = render(<ZHNumberValue value={undefined} precision="quantity" />);
    expect(root(u.container).textContent).toBe("—");
  });

  it("cero NO es vacío: quantity 4 → 0.0000", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(<ZHNumberValue value={0} precision="quantity" />);
    expect(root(container).textContent).toBe("0.0000");
    expect(root(container).className).not.toContain("--empty");
  });

  it("negativos conservan el signo numérico normal (-5.0000)", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(<ZHNumberValue value={-5} precision="quantity" />);
    expect(root(container).textContent).toBe("-5.0000");
  });

  it("prefix y suffix en sus propios spans BEM, alrededor de __amount", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(
      <ZHNumberValue value={15} precision="percentage" prefix="≈" suffix="%" />,
    );
    const el = root(container);
    expect([...el.children].map((c) => c.className)).toEqual([
      "zh-number-value__prefix",
      "zh-number-value__amount",
      "zh-number-value__suffix",
    ]);
    expect(el.textContent).toBe("≈15.00%");
  });

  it("sin prefix/suffix solo renderiza __amount", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(<ZHNumberValue value={1} precision="quantity" />);
    expect(root(container).children).toHaveLength(1);
  });

  it("locale explícito y ZHLocaleProvider aplican solo representación", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(<ZHNumberValue value={1234.5} precision="quantity" locale="en-US" />);
    expect(amount(container)).toBe("1,234.5000");
    cleanup();
    const p = render(
      <ZHLocaleProvider locale="en-US">
        <ZHNumberValue value={1234.5} precision="percentage" />
      </ZHLocaleProvider>,
    );
    expect(amount(p.container)).toBe("1,234.50");
  });

  it("clases: default emphasis/align end, emphasis/align override y className; sin style inline", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(<ZHNumberValue value={1} precision="quantity" />);
    expect(root(container).className).toBe(
      "zh-number-value zh-number-value--default zh-number-value--end",
    );
    expect(root(container).hasAttribute("style")).toBe(false);
    cleanup();
    const o = render(
      <ZHNumberValue value={1} precision="quantity" emphasis="strong" align="start" className="x" />,
    );
    expect(root(o.container).className).toBe(
      "zh-number-value zh-number-value--strong zh-number-value--start x",
    );
  });

  it("no muta el value recibido", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const props = Object.freeze({ value: 0.075, decimals: 2 });
    const { container } = render(<ZHNumberValue {...props} />);
    expect(amount(container)).toBe("0.08");
    expect(props.value).toBe(0.075);
  });

  it("CSS del DS: align end y tabular-nums compartidos con ZHMoneyValue (zh-ui.css)", () => {
    const css = readFileSync(resolve(__dirname, "../../styles/zh-ui.css"), "utf-8").replace(/\r\n/g, "\n");
    const rule = (selector: string) => {
      const match = css.match(new RegExp(`(?:^|\\n)([^{}]*${selector.replace(/[.-]/g, "\\$&")}[^{}]*)\\{([^}]*)\\}`));
      return match?.[2] ?? "";
    };
    expect(rule(".zh-number-value--end")).toContain("justify-content: flex-end");
    expect(rule(".zh-number-value__amount")).toContain("font-variant-numeric: tabular-nums");
    expect(rule(".zh-number-value__amount")).toBe(rule(".zh-money-value__amount"));
  });
});

describe("motor único: ZHMoneyValue, ZHNumberValue y formatMoney convergen (02A)", () => {
  it.each([
    [0.075, 2], [0.305, 2], [1.005, 2], [-0.075, 2], [0, 2],
    [0.12345, 4], [0.2261, 6], [1.123456785, 8], [0.0045783210, 10],
  ])("%s a %s decimales produce el mismo texto en los tres caminos", (value, decimals) => {
    const expected = formatDecimalDisplay(value, decimals);
    const money = render(<ZHMoneyValue value={value} decimals={decimals} />);
    const number = render(<ZHNumberValue value={value} decimals={decimals} />);
    expect(money.container.querySelector(".zh-money-value__amount")?.textContent).toBe(expected);
    expect(amount(number.container)).toBe(expected);
    expect(formatMoney(value, decimals)).toBe(expected);
  });
});
