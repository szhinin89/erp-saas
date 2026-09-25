// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { act, render, screen, cleanup } from "@testing-library/react";
import { ZHMoneyValue } from "./ZHMoneyValue";
import { ZHLocaleProvider } from "./ZHLocaleProvider";
import {
  setPrecisionPolicyForTests,
  type PrecisionPolicy,
} from "../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../test/precisionPolicyFixture";

afterEach(() => {
  cleanup();
});

const fmt = (locale: string, value: number, decimals = 2) =>
  new Intl.NumberFormat(locale, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(value);

describe("ZHMoneyValue", () => {
  it('renderiza "$" por defecto', () => {
    render(<ZHMoneyValue precision="money" value={100} />);
    expect(document.querySelector(".zh-money-value__symbol")?.textContent).toBe(
      "$",
    );
  });

  it("sin provider ni prop mantiene fallback estable (formatMoney, punto decimal)", () => {
    render(<ZHMoneyValue precision="money" value={100} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "100.00",
    );
  });

  it('con locale prop="en-US" formatea 1299.5 como 1,299.50', () => {
    render(<ZHMoneyValue precision="money" value={1299.5} locale="en-US" />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "1,299.50",
    );
  });

  it('con locale prop="es-EC" formatea usando Intl para es-EC', () => {
    render(<ZHMoneyValue precision="money" value={299.9} locale="es-EC" />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      fmt("es-EC", 299.9),
    );
  });

  it('con provider locale="en-US", ZHMoneyValue usa ese locale', () => {
    render(
      <ZHLocaleProvider locale="en-US">
        <ZHMoneyValue precision="money" value={1299.5} />
      </ZHLocaleProvider>,
    );
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "1,299.50",
    );
  });

  it("locale prop tiene prioridad sobre provider", () => {
    render(
      <ZHLocaleProvider locale="en-US">
        <ZHMoneyValue precision="money" value={1299.5} locale="es-EC" />
      </ZHLocaleProvider>,
    );
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      fmt("es-EC", 1299.5),
    );
  });

  it("permite currencySymbol personalizado", () => {
    render(<ZHMoneyValue precision="money" value={100} currencySymbol="USD " />);
    expect(document.querySelector(".zh-money-value__symbol")?.textContent).toBe(
      "USD ",
    );
  });

  it('value={null} no intenta formatear y renderiza "—" sin símbolo', () => {
    render(<ZHMoneyValue precision="money" value={null} />);
    expect(screen.getByText("—")).toBeTruthy();
    expect(document.querySelector(".zh-money-value__symbol")).toBeNull();
    expect(document.querySelector(".zh-money-value__amount")).toBeNull();
  });

  it('value={undefined} no intenta formatear y renderiza "—" sin símbolo', () => {
    render(<ZHMoneyValue precision="money" value={undefined} />);
    expect(screen.getByText("—")).toBeTruthy();
    expect(document.querySelector(".zh-money-value__symbol")).toBeNull();
    expect(document.querySelector(".zh-money-value__amount")).toBeNull();
  });

  it("emphasis=default aplica .zh-money-value--default", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={100} emphasis="default" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--default"),
    ).toBe(true);
  });

  it("emphasis=strong aplica .zh-money-value--strong", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={100} emphasis="strong" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--strong"),
    ).toBe(true);
  });

  it("emphasis=total aplica .zh-money-value--total", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={100} emphasis="total" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--total"),
    ).toBe(true);
  });

  it("emphasis=muted aplica .zh-money-value--muted", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={100} emphasis="muted" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--muted"),
    ).toBe(true);
  });

  it("emphasis=grand aplica .zh-money-value--grand", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={100} emphasis="grand" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--grand"),
    ).toBe(true);
  });

  it("align=start aplica .zh-money-value--start", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={100} align="start" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--start"),
    ).toBe(true);
  });

  it("align=end aplica .zh-money-value--end por defecto", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={100} />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--end"),
    ).toBe(true);
  });

  it("className adicional se combina correctamente", () => {
    const { container } = render(
      <ZHMoneyValue precision="money" value={100} className="custom-class" />,
    );
    const el = container.firstElementChild;
    expect(el?.className.includes("zh-money-value")).toBe(true);
    expect(el?.className.includes("custom-class")).toBe(true);
  });

  it("no hay atributo style en el elemento renderizado", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={100} />);
    expect(container.firstElementChild?.hasAttribute("style")).toBe(false);
  });
});

describe("ZHMoneyValue — escala resuelta por la policy (money)", () => {

  it("decimals={0} muestra sin decimales", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 0 });
    render(<ZHMoneyValue precision="money" value={100} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "100",
    );
  });

  it("decimals={3} muestra 3 decimales", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 });
    render(<ZHMoneyValue precision="money" value={100} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "100.000",
    );
  });

  it("decimals={4} muestra 4 decimales", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 4 });
    render(<ZHMoneyValue precision="money" value={24.3041} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "24.3041",
    );
  });

  it('value={null} ignora decimals y muestra "—"', () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 4 });
    render(<ZHMoneyValue precision="money" value={null} />);
    expect(screen.getByText("—")).toBeTruthy();
    expect(document.querySelector(".zh-money-value__amount")).toBeNull();
  });

  it('value={undefined} ignora decimals y muestra "—"', () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 4 });
    render(<ZHMoneyValue precision="money" value={undefined} />);
    expect(screen.getByText("—")).toBeTruthy();
    expect(document.querySelector(".zh-money-value__amount")).toBeNull();
  });

  it("con locale=en-US y decimals={3}, formatea con 3 decimales", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 });
    render(<ZHMoneyValue precision="money" value={1299.5} locale="en-US" />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      fmt("en-US", 1299.5, 3),
    );
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "1,299.500",
    );
  });

  it("con locale=es-EC y decimals={3}, formatea con 3 decimales", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 });
    render(<ZHMoneyValue precision="money" value={1299.5} locale="es-EC" />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      fmt("es-EC", 1299.5, 3),
    );
  });

  it.each([undefined, "en-US"])("respects 10 decimals with locale %s", (locale) => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 10 });
    render(<ZHMoneyValue precision="money" value={0.0045783210} locale={locale} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe("0.0045783210");
  });

  it("does not impose a frontend maximum", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 12 });
    render(<ZHMoneyValue precision="money" value={100} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe("100.000000000000");
  });

  it("currencySymbol sigue renderizando con decimals custom", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 4 });
    render(<ZHMoneyValue precision="money" value={100} currencySymbol="USD " />);
    expect(document.querySelector(".zh-money-value__symbol")?.textContent).toBe(
      "USD ",
    );
  });

  it("emphasis/align siguen funcionando con decimals custom", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 4 });
    const { container } = render(
      <ZHMoneyValue precision="money" value={100} emphasis="total" align="start" />,
    );
    expect(
      container.firstElementChild?.className.includes("zh-money-value--total"),
    ).toBe(true);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--start"),
    ).toBe(true);
  });

  it("className sigue combinándose con decimals custom", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 4 });
    const { container } = render(
      <ZHMoneyValue precision="money" value={100} className="custom-class" />,
    );
    expect(container.firstElementChild?.className.includes("custom-class")).toBe(
      true,
    );
  });

  it("no hay style inline con decimals custom", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 4 });
    const { container } = render(<ZHMoneyValue precision="money" value={100} />);
    expect(container.firstElementChild?.hasAttribute("style")).toBe(false);
  });
});

/**
 * ZH-DESIGN-SYSTEM-PRECISION-01A — LEGACY CHARACTERIZATION.
 * Congela el contrato REAL actual antes de la migración a precisión semántica.
 * - "contract:" → comportamiento público que debe conservarse.
 * - "legacy:"   → deuda caracterizada (default decimals=2, "$-5.00", ruta Intl con locale);
 *                 NO es la arquitectura futura deseada ni una regla del ERP.
 */
describe("ZHMoneyValue — characterization 01A", () => {
  const amount = () => document.querySelector(".zh-money-value__amount")?.textContent;
  const root = (container: HTMLElement) => container.firstElementChild as HTMLElement;

  it("contract: estructura DOM exacta — span raíz con __symbol y __amount en ese orden", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={10} />);
    const el = root(container);
    expect(el.tagName).toBe("SPAN");
    expect(el.className).toBe("zh-money-value zh-money-value--default zh-money-value--end");
    expect(el.children).toHaveLength(2);
    expect(el.children[0]?.className).toBe("zh-money-value__symbol");
    expect(el.children[1]?.className).toBe("zh-money-value__amount");
    expect(el.textContent).toBe("$10.00");
  });

  it("contract: vacío agrega --empty, conserva modificadores y className, texto '—'", () => {
    const { container } = render(
      <ZHMoneyValue precision="money" value={null} emphasis="total" align="start" className="x" />,
    );
    const el = root(container);
    expect(el.className).toBe(
      "zh-money-value zh-money-value--total zh-money-value--start zh-money-value--empty x",
    );
    expect(el.textContent).toBe("—");
    expect(el.children).toHaveLength(0);
  });


  it.each([
    [0, "12"],
    [2, "12.35"],
    [4, "12.3457"],
    [6, "12.345678"],
  ])("contract: decimals={%s} formatea 12.345678 como %s", (decimals, expected) => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: decimals });
    render(<ZHMoneyValue precision="money" value={12.345678} />);
    expect(amount()).toBe(expected);
  });

  it("contract: cero se muestra con símbolo y decimales ($0.00), no como vacío", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={0} />);
    expect(root(container).textContent).toBe("$0.00");
    expect(root(container).className.includes("zh-money-value--empty")).toBe(false);
  });

  it("contract: positivos sin separador de miles en la ruta sin locale", () => {
    render(<ZHMoneyValue precision="money" value={1234567.5} />);
    expect(amount()).toBe("1234567.50");
  });

  it("legacy: negativos se muestran como '$-5.00' (símbolo antes del signo)", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={-5} />);
    expect(root(container).textContent).toBe("$-5.00");
    expect(amount()).toBe("-5.00");
  });

  it.each([
    [0.075, "0.08"],
    [0.305, "0.31"],
    [-0.075, "-0.08"],
    [1.005, "1.01"],
  ])("contract: sin locale, midpoint %s con 2 decimales → %s (Decimal ROUND_HALF_UP)", (value, expected) => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    render(<ZHMoneyValue precision="money" value={value} />);
    expect(amount()).toBe(expected);
  });

  it("contract: currencySymbol vacío renderiza __symbol vacío (el span sigue existiendo)", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={3} currencySymbol="" />);
    const symbol = document.querySelector(".zh-money-value__symbol");
    expect(symbol).not.toBeNull();
    expect(symbol?.textContent).toBe("");
    expect(root(container).textContent).toBe("3.00");
  });

  it("legacy: con locale explícito usa Intl.NumberFormat — agrega separador de miles", () => {
    render(<ZHMoneyValue precision="money" value={1234567.5} locale="en-US" />);
    expect(amount()).toBe("1,234,567.50");
  });

  it("legacy: con locale, negativos → '$-5.00' (mismo orden símbolo/signo)", () => {
    const { container } = render(<ZHMoneyValue precision="money" value={-5} locale="en-US" />);
    expect(root(container).textContent).toBe("$-5.00");
  });

  it.each([
    [1.005, "1.01"],
    [0.075, "0.08"],
    [-0.075, "-0.08"],
  ])("legacy: con locale, midpoint %s se delega a Intl → %s (hoy coincide con la ruta sin locale)", (value, expected) => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    render(<ZHMoneyValue precision="money" value={value} locale="en-US" />);
    expect(amount()).toBe(fmt("en-US", value, 2));
    expect(amount()).toBe(expected);
  });

  it("legacy: ZHLocaleProvider activa la misma ruta Intl que la prop locale", () => {
    render(
      <ZHLocaleProvider locale="en-US">
        <ZHMoneyValue precision="money" value={1234.5} />
      </ZHLocaleProvider>,
    );
    expect(amount()).toBe("1,234.50");
  });
});

/** ZH-DESIGN-SYSTEM-PRECISION-02A — precisión semántica (`precision`) sobre la PrecisionPolicy. */
describe("ZHMoneyValue — precision semántica (02A)", () => {
  const amount = () => document.querySelector(".zh-money-value__amount")?.textContent;
  const text = () => document.querySelector(".zh-money-value")?.textContent;

  const POLICY_A: PrecisionPolicy = {
    ...TEST_PRECISION_POLICY,
    unitCostDecimals: 6,
    averageCostDecimals: 8,
    salesUnitPriceDecimals: 4,
    purchaseUnitPriceDecimals: 5,
    moneyDecimals: 2,
    taxDecimals: 3,
    accountingDecimals: 4,
  };
  const POLICY_B: PrecisionPolicy = { ...POLICY_A, unitCostDecimals: 4 };

  it("unitCost=6: 0.2261 → $0.226100", () => {
    setPrecisionPolicyForTests(POLICY_A);
    render(<ZHMoneyValue value={0.2261} precision="unitCost" />);
    expect(text()).toBe("$0.226100");
  });

  it("reactivo sin remount: policy A (unitCost 6) → B (unitCost 4): $0.226100 → $0.2261", () => {
    setPrecisionPolicyForTests(POLICY_A);
    render(<ZHMoneyValue value={0.2261} precision="unitCost" />);
    const node = document.querySelector(".zh-money-value__amount");
    expect(text()).toBe("$0.226100");
    act(() => setPrecisionPolicyForTests(POLICY_B));
    expect(document.querySelector(".zh-money-value__amount")).toBe(node);
    expect(text()).toBe("$0.2261");
  });

  it.each<[Parameters<typeof ZHMoneyValue>[0]["precision"], number, string]>([
    ["salesUnitPrice", 0.3, "0.3000"],
    ["purchaseUnitPrice", 0.3, "0.30000"],
    ["averageCost", 0.3, "0.30000000"],
    ["money", 0.3, "0.30"],
    ["tax", 0.3, "0.300"],
    ["accounting", 0.3, "0.3000"],
  ])("%s lee su escala de la policy vía resolver (%s → %s)", (precision, value, expected) => {
    setPrecisionPolicyForTests(POLICY_A);
    render(<ZHMoneyValue value={value} precision={precision} />);
    expect(amount()).toBe(expected);
  });




  it("null/undefined con precision → '—' sin símbolo", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(<ZHMoneyValue value={null} precision="unitCost" />);
    expect(container.textContent).toBe("—");
    expect(container.firstElementChild?.className).toContain("zh-money-value--empty");
    cleanup();
    const u = render(<ZHMoneyValue value={undefined} precision="unitCost" />);
    expect(u.container.textContent).toBe("—");
  });

  it("cero con precision se muestra con la escala semántica ($0.000000), no como vacío", () => {
    setPrecisionPolicyForTests(POLICY_A);
    render(<ZHMoneyValue value={0} precision="unitCost" />);
    expect(text()).toBe("$0.000000");
  });

  it("negativos con precision conservan el contrato legacy '$-…'", () => {
    setPrecisionPolicyForTests(POLICY_A);
    render(<ZHMoneyValue value={-5} precision="salesUnitPrice" />);
    expect(text()).toBe("$-5.0000");
  });

  it("locale explícito con precision: representación Intl sobre la escala semántica", () => {
    setPrecisionPolicyForTests(POLICY_A);
    render(<ZHMoneyValue value={1234.5} precision="salesUnitPrice" locale="en-US" />);
    expect(amount()).toBe("1,234.5000");
  });

  it("DOM/clases idénticos al contrato legacy; align end por defecto; importe en __amount (tabular-nums)", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { container } = render(<ZHMoneyValue value={1} precision="money" />);
    const el = container.firstElementChild as HTMLElement;
    expect(el.className).toBe("zh-money-value zh-money-value--default zh-money-value--end");
    expect([...el.children].map((c) => c.className)).toEqual([
      "zh-money-value__symbol",
      "zh-money-value__amount",
    ]);
  });
});

// ZH-DESIGN-SYSTEM-PRECISION-05B — la firma sin precision/decimals queda @deprecated (sobrecarga);
// el runtime sigue siendo decimals > precision > legacy 2.

// ZH-DESIGN-SYSTEM-PRECISION-06 — API única: `precision` obligatorio; sin `decimals` público ni
// default legacy. La escala solo sale de la PrecisionPolicy (resolvePrecisionDecimals).
describe("ZHMoneyValue — API única (06)", () => {
  it("compile-time: sin precision o con decimals no compila; con precision sí", () => {
    // @ts-expect-error — sin `precision` no compila: no existe default 2.
    const withoutPrecision = <ZHMoneyValue value={1} />;
    // @ts-expect-error — `decimals` ya no es API pública.
    const withDecimals = <ZHMoneyValue value={1} decimals={2} />;
    const semantic = <ZHMoneyValue value={1} precision="money" />;
    expect([withoutPrecision, withDecimals, semantic]).toHaveLength(3);
  });

  it("la escala sigue a la policy: money 3 → $1.500", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 });
    render(<ZHMoneyValue value={1.5} precision="money" />);
    expect(screen.getByText("1.500")).toBeTruthy();
  });
});
