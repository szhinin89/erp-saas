// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { ZHMoneyValue } from "./ZHMoneyValue";
import { ZHLocaleProvider } from "./ZHLocaleProvider";

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
    render(<ZHMoneyValue value={100} />);
    expect(document.querySelector(".zh-money-value__symbol")?.textContent).toBe(
      "$",
    );
  });

  it("sin provider ni prop mantiene fallback estable (formatMoney, punto decimal)", () => {
    render(<ZHMoneyValue value={100} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "100.00",
    );
  });

  it('con locale prop="en-US" formatea 1299.5 como 1,299.50', () => {
    render(<ZHMoneyValue value={1299.5} locale="en-US" />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "1,299.50",
    );
  });

  it('con locale prop="es-EC" formatea usando Intl para es-EC', () => {
    render(<ZHMoneyValue value={299.9} locale="es-EC" />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      fmt("es-EC", 299.9),
    );
  });

  it('con provider locale="en-US", ZHMoneyValue usa ese locale', () => {
    render(
      <ZHLocaleProvider locale="en-US">
        <ZHMoneyValue value={1299.5} />
      </ZHLocaleProvider>,
    );
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "1,299.50",
    );
  });

  it("locale prop tiene prioridad sobre provider", () => {
    render(
      <ZHLocaleProvider locale="en-US">
        <ZHMoneyValue value={1299.5} locale="es-EC" />
      </ZHLocaleProvider>,
    );
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      fmt("es-EC", 1299.5),
    );
  });

  it("permite currencySymbol personalizado", () => {
    render(<ZHMoneyValue value={100} currencySymbol="USD " />);
    expect(document.querySelector(".zh-money-value__symbol")?.textContent).toBe(
      "USD ",
    );
  });

  it('value={null} no intenta formatear y renderiza "—" sin símbolo', () => {
    render(<ZHMoneyValue value={null} />);
    expect(screen.getByText("—")).toBeTruthy();
    expect(document.querySelector(".zh-money-value__symbol")).toBeNull();
    expect(document.querySelector(".zh-money-value__amount")).toBeNull();
  });

  it('value={undefined} no intenta formatear y renderiza "—" sin símbolo', () => {
    render(<ZHMoneyValue value={undefined} />);
    expect(screen.getByText("—")).toBeTruthy();
    expect(document.querySelector(".zh-money-value__symbol")).toBeNull();
    expect(document.querySelector(".zh-money-value__amount")).toBeNull();
  });

  it("emphasis=default aplica .zh-money-value--default", () => {
    const { container } = render(<ZHMoneyValue value={100} emphasis="default" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--default"),
    ).toBe(true);
  });

  it("emphasis=strong aplica .zh-money-value--strong", () => {
    const { container } = render(<ZHMoneyValue value={100} emphasis="strong" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--strong"),
    ).toBe(true);
  });

  it("emphasis=total aplica .zh-money-value--total", () => {
    const { container } = render(<ZHMoneyValue value={100} emphasis="total" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--total"),
    ).toBe(true);
  });

  it("emphasis=muted aplica .zh-money-value--muted", () => {
    const { container } = render(<ZHMoneyValue value={100} emphasis="muted" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--muted"),
    ).toBe(true);
  });

  it("emphasis=grand aplica .zh-money-value--grand", () => {
    const { container } = render(<ZHMoneyValue value={100} emphasis="grand" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--grand"),
    ).toBe(true);
  });

  it("align=start aplica .zh-money-value--start", () => {
    const { container } = render(<ZHMoneyValue value={100} align="start" />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--start"),
    ).toBe(true);
  });

  it("align=end aplica .zh-money-value--end por defecto", () => {
    const { container } = render(<ZHMoneyValue value={100} />);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--end"),
    ).toBe(true);
  });

  it("className adicional se combina correctamente", () => {
    const { container } = render(
      <ZHMoneyValue value={100} className="custom-class" />,
    );
    const el = container.firstElementChild;
    expect(el?.className.includes("zh-money-value")).toBe(true);
    expect(el?.className.includes("custom-class")).toBe(true);
  });

  it("no hay atributo style en el elemento renderizado", () => {
    const { container } = render(<ZHMoneyValue value={100} />);
    expect(container.firstElementChild?.hasAttribute("style")).toBe(false);
  });
});

describe("ZHMoneyValue — decimals", () => {
  it("sin decimals mantiene 2 decimales", () => {
    render(<ZHMoneyValue value={100} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "100.00",
    );
  });

  it("decimals={0} muestra sin decimales", () => {
    render(<ZHMoneyValue value={100} decimals={0} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "100",
    );
  });

  it("decimals={3} muestra 3 decimales", () => {
    render(<ZHMoneyValue value={100} decimals={3} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "100.000",
    );
  });

  it("decimals={4} muestra 4 decimales", () => {
    render(<ZHMoneyValue value={24.3041} decimals={4} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "24.3041",
    );
  });

  it('value={null} ignora decimals y muestra "—"', () => {
    render(<ZHMoneyValue value={null} decimals={4} />);
    expect(screen.getByText("—")).toBeTruthy();
    expect(document.querySelector(".zh-money-value__amount")).toBeNull();
  });

  it('value={undefined} ignora decimals y muestra "—"', () => {
    render(<ZHMoneyValue value={undefined} decimals={4} />);
    expect(screen.getByText("—")).toBeTruthy();
    expect(document.querySelector(".zh-money-value__amount")).toBeNull();
  });

  it("con locale=en-US y decimals={3}, formatea con 3 decimales", () => {
    render(<ZHMoneyValue value={1299.5} locale="en-US" decimals={3} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      fmt("en-US", 1299.5, 3),
    );
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "1,299.500",
    );
  });

  it("con locale=es-EC y decimals={3}, formatea con 3 decimales", () => {
    render(<ZHMoneyValue value={1299.5} locale="es-EC" decimals={3} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      fmt("es-EC", 1299.5, 3),
    );
  });

  it("decimals negativo usa fallback seguro (se recorta a 0)", () => {
    render(<ZHMoneyValue value={100} decimals={-2} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "100",
    );
  });

  it("decimals excesivo se limita (recorte a 6)", () => {
    render(<ZHMoneyValue value={100} decimals={12} />);
    expect(document.querySelector(".zh-money-value__amount")?.textContent).toBe(
      "100.000000",
    );
  });

  it("currencySymbol sigue renderizando con decimals custom", () => {
    render(<ZHMoneyValue value={100} decimals={4} currencySymbol="USD " />);
    expect(document.querySelector(".zh-money-value__symbol")?.textContent).toBe(
      "USD ",
    );
  });

  it("emphasis/align siguen funcionando con decimals custom", () => {
    const { container } = render(
      <ZHMoneyValue value={100} decimals={4} emphasis="total" align="start" />,
    );
    expect(
      container.firstElementChild?.className.includes("zh-money-value--total"),
    ).toBe(true);
    expect(
      container.firstElementChild?.className.includes("zh-money-value--start"),
    ).toBe(true);
  });

  it("className sigue combinándose con decimals custom", () => {
    const { container } = render(
      <ZHMoneyValue value={100} decimals={4} className="custom-class" />,
    );
    expect(container.firstElementChild?.className.includes("custom-class")).toBe(
      true,
    );
  });

  it("no hay style inline con decimals custom", () => {
    const { container } = render(<ZHMoneyValue value={100} decimals={4} />);
    expect(container.firstElementChild?.hasAttribute("style")).toBe(false);
  });
});
