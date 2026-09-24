// @vitest-environment jsdom
/**
 * ZH-DESIGN-SYSTEM-PRECISION-01A — tests de CARACTERIZACIÓN de ZhCurrencyInput.
 *
 * Congelan el comportamiento REAL actual antes de migrar el Design System de precisión.
 * - "contract:" → comportamiento público que debe conservarse.
 * - "legacy:"   → deuda caracterizada (default decimals=2, sin normalización en blur, value sin
 *                 formatear). Se congela temporalmente; NO es una regla del ERP.
 */
import React from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render } from "@testing-library/react";
import { ZhCurrencyInput } from "./ZhCurrencyInput";

afterEach(() => {
  cleanup();
});

function renderInput(props: React.ComponentProps<typeof ZhCurrencyInput> = {}) {
  const utils = render(<ZhCurrencyInput aria-label="monto" {...props} />);
  const input = utils.getByLabelText("monto") as HTMLInputElement;
  const wrapper = input.parentElement as HTMLElement;
  return { ...utils, input, wrapper };
}

function typeRaw(input: HTMLInputElement, value: string, caret = value.length) {
  fireEvent.change(input, { target: { value } });
  input.setSelectionRange(caret, caret);
}

const keyAllowed = (input: HTMLInputElement, key: string) => fireEvent.keyDown(input, { key });

const paste = (input: HTMLInputElement, text: string) =>
  fireEvent.paste(input, { clipboardData: { getData: () => text } });

describe("ZhCurrencyInput — DOM y props (contract)", () => {
  it("contract: wrapper zh-prefixed-input con prefijo aria-hidden y input zh-numeric-input", () => {
    const { input, wrapper } = renderInput();
    expect(wrapper.tagName).toBe("DIV");
    expect(wrapper.className).toBe("zh-prefixed-input");
    const prefix = wrapper.querySelector(".zh-input-prefix");
    expect(prefix?.getAttribute("aria-hidden")).toBe("true");
    expect(wrapper.firstElementChild).toBe(prefix);
    expect(input.type).toBe("text");
    expect(input.getAttribute("inputmode")).toBe("decimal");
    expect(input.className).toBe("zh-numeric-input");
  });

  it("contract: currency default = USD", () => {
    const { wrapper } = renderInput();
    expect(wrapper.querySelector(".zh-input-prefix")?.textContent).toBe("USD");
  });

  it("contract: currency personalizada", () => {
    const { wrapper } = renderInput({ currency: "$" });
    expect(wrapper.querySelector(".zh-input-prefix")?.textContent).toBe("$");
  });

  it("contract: className se agrega después de zh-numeric-input", () => {
    const { input } = renderInput({ className: "extra" });
    expect(input.className).toBe("zh-numeric-input extra");
  });

  it("contract: disabled deshabilita el input y agrega zh-prefixed-input--disabled al wrapper", () => {
    const { input, wrapper } = renderInput({ disabled: true });
    expect(input.disabled).toBe(true);
    expect(wrapper.className).toBe("zh-prefixed-input zh-prefixed-input--disabled");
  });

  it("contract: readOnly se pasa al input nativo sin clase adicional en el wrapper", () => {
    const { input, wrapper } = renderInput({ readOnly: true });
    expect(input.readOnly).toBe(true);
    expect(wrapper.className).toBe("zh-prefixed-input");
  });

  it("contract: forwardRef expone el HTMLInputElement", () => {
    const ref = React.createRef<HTMLInputElement>();
    render(<ZhCurrencyInput ref={ref} aria-label="monto" />);
    expect(ref.current).toBeInstanceOf(HTMLInputElement);
  });
});

describe("ZhCurrencyInput — value", () => {
  it("legacy: value numérico NO se formatea con decimals (5 → '5')", () => {
    const { input } = renderInput({ value: 5, decimals: 4, onChange: () => {} });
    expect(input.value).toBe("5");
  });

  it("contract: value string se muestra tal cual", () => {
    const { input } = renderInput({ value: "12.5", onChange: () => {} });
    expect(input.value).toBe("12.5");
  });

  it("contract: sin value queda vacío", () => {
    const { input } = renderInput();
    expect(input.value).toBe("");
  });
});

describe("ZhCurrencyInput — teclado", () => {
  it("contract: permite dígitos y bloquea caracteres inválidos", () => {
    const { input } = renderInput();
    expect(keyAllowed(input, "7")).toBe(true);
    expect(keyAllowed(input, "a")).toBe(false);
    expect(keyAllowed(input, "e")).toBe(false);
  });

  it("contract: siempre positivo — bloquea '-' incluso al inicio", () => {
    const { input } = renderInput();
    expect(keyAllowed(input, "-")).toBe(false);
  });

  it("legacy: default decimals=2 limita los dígitos tras el punto", () => {
    const { input } = renderInput();
    typeRaw(input, "1.2");
    expect(keyAllowed(input, "3")).toBe(true);
    typeRaw(input, "1.23");
    expect(keyAllowed(input, "4")).toBe(false);
  });

  it("contract: decimals explícito amplía el límite", () => {
    const { input } = renderInput({ decimals: 4 });
    typeRaw(input, "1.234");
    expect(keyAllowed(input, "5")).toBe(true);
    typeRaw(input, "1.2345");
    expect(keyAllowed(input, "6")).toBe(false);
  });

  it("contract: decimals=0 bloquea el punto", () => {
    const { input } = renderInput({ decimals: 0 });
    expect(keyAllowed(input, ".")).toBe(false);
  });

  it("contract: invoca onKeyDown del consumidor", () => {
    const onKeyDown = vi.fn();
    const { input } = renderInput({ onKeyDown });
    keyAllowed(input, "a");
    expect(onKeyDown).toHaveBeenCalledTimes(1);
  });
});

describe("ZhCurrencyInput — paste", () => {
  it("contract: pega valor válido y dispara onChange programático", () => {
    const onChange = vi.fn();
    const { input } = renderInput({ onChange });
    expect(paste(input, "99.95")).toBe(false);
    expect(input.value).toBe("99.95");
    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it("legacy: exceso de decimales en paste se TRUNCA con default decimals=2", () => {
    const { input } = renderInput();
    paste(input, "1.239");
    expect(input.value).toBe("1.23");
  });

  it("contract: paste negativo descarta el signo (siempre positivo)", () => {
    const { input } = renderInput();
    paste(input, "-5.5");
    expect(input.value).toBe("5.5");
  });

  it("contract: paste sin dígitos no cambia el valor ni dispara onChange", () => {
    const onChange = vi.fn();
    const { input } = renderInput({ onChange });
    paste(input, "abc");
    expect(input.value).toBe("");
    expect(onChange).not.toHaveBeenCalled();
  });

  it("contract: invoca onPaste del consumidor", () => {
    const onPaste = vi.fn();
    const { input } = renderInput({ onPaste });
    paste(input, "1");
    expect(onPaste).toHaveBeenCalledTimes(1);
  });
});

describe("ZhCurrencyInput — focus / blur / onChange", () => {
  it("contract: focus no modifica el valor ni dispara onChange", () => {
    const onChange = vi.fn();
    const { input } = renderInput({ defaultValue: "5", onChange });
    fireEvent.focus(input);
    expect(input.value).toBe("5");
    expect(onChange).not.toHaveBeenCalled();
  });

  it("legacy: blur NO aplica formatter ('5' sigue '5', sin onChange)", () => {
    const onChange = vi.fn();
    const onBlur = vi.fn();
    const { input } = renderInput({ decimals: 2, onChange, onBlur });
    typeRaw(input, "5");
    onChange.mockClear();
    fireEvent.blur(input);
    expect(input.value).toBe("5");
    expect(onChange).not.toHaveBeenCalled();
    expect(onBlur).toHaveBeenCalledTimes(1);
  });

  it("legacy: blur NO normaliza decimales faltantes ('12.2' sigue '12.2')", () => {
    const { input } = renderInput({ decimals: 4 });
    typeRaw(input, "12.2");
    fireEvent.blur(input);
    expect(input.value).toBe("12.2");
  });

  it("contract: onChange se dispara en cada edición del usuario", () => {
    const onChange = vi.fn();
    const { input } = renderInput({ onChange });
    fireEvent.change(input, { target: { value: "1" } });
    fireEvent.change(input, { target: { value: "1.5" } });
    expect(onChange).toHaveBeenCalledTimes(2);
  });
});
