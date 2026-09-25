// @vitest-environment jsdom
/**
 * ZH-DESIGN-SYSTEM-PRECISION-01A — tests de CARACTERIZACIÓN de ZhDecimalInput.
 *
 * Congelan el comportamiento REAL actual antes de migrar el Design System de precisión.
 * - "contract:" → comportamiento público que debe conservarse.
 * - "legacy:"   → deuda caracterizada (p. ej. Number.toFixed binario, truncado en paste). Se
 *                 congela temporalmente para que su futura migración al formatter SSOT sea
 *                 deliberada y verificable; NO es una regla del ERP.
 */
import React from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render } from "@testing-library/react";
import { useForm } from "react-hook-form";
import { ZhDecimalInput } from "./ZhDecimalInput";
import { formatMoney } from "../../../lib/sanitizers";
import {
  setPrecisionPolicyForTests,
  type PrecisionPolicy,
} from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

afterEach(() => {
  cleanup();
});

function renderInput(props: React.ComponentProps<typeof ZhDecimalInput> = {}) {
  const utils = render(<ZhDecimalInput aria-label="valor" {...props} />);
  const input = utils.getByLabelText("valor") as HTMLInputElement;
  return { ...utils, input };
}

/** Deja un valor en el DOM y posiciona el cursor (sin pasar por el formateo del componente). */
function typeRaw(input: HTMLInputElement, value: string, caret = value.length) {
  fireEvent.change(input, { target: { value } });
  input.setSelectionRange(caret, caret);
}

/** fireEvent devuelve false cuando el handler llamó preventDefault (tecla bloqueada). */
const keyAllowed = (input: HTMLInputElement, key: string, init: object = {}) =>
  fireEvent.keyDown(input, { key, ...init });

const paste = (input: HTMLInputElement, text: string) =>
  fireEvent.paste(input, { clipboardData: { getData: () => text } });

describe("ZhDecimalInput — DOM y props (contract)", () => {
  it("contract: renderiza input type=text, inputMode=decimal, clase zh-numeric-input", () => {
    const { input } = renderInput();
    expect(input.tagName).toBe("INPUT");
    expect(input.type).toBe("text");
    expect(input.getAttribute("inputmode")).toBe("decimal");
    expect(input.className).toBe("zh-numeric-input");
  });

  it("contract: density=compact agrega zh-input--compact y className se combina al final", () => {
    const { input } = renderInput({ density: "compact", className: "extra" });
    expect(input.className).toBe("zh-numeric-input zh-input--compact extra");
  });

  it("contract: forwardRef expone el HTMLInputElement", () => {
    const ref = React.createRef<HTMLInputElement>();
    render(<ZhDecimalInput ref={ref} aria-label="valor" />);
    expect(ref.current).toBeInstanceOf(HTMLInputElement);
  });

  it("contract: readOnly y disabled se pasan tal cual al input nativo", () => {
    const { input: ro } = renderInput({ readOnly: true });
    expect(ro.readOnly).toBe(true);
    cleanup();
    const { input: dis } = renderInput({ disabled: true });
    expect(dis.disabled).toBe(true);
    expect(dis.className).toBe("zh-numeric-input");
  });

  it("contract: sin value ni defaultValue el input queda vacío", () => {
    const { input } = renderInput();
    expect(input.value).toBe("");
  });
});

describe("ZhDecimalInput — value / defaultValue", () => {
  it("legacy: default decimals=2 al formatear value numérico", () => {
    const { input } = renderInput({ value: 5, onChange: () => {} });
    expect(input.value).toBe("5.00");
  });

  it.each([
    [0, 12.3456, "12"],
    [2, 12.3456, "12.35"],
    [4, 12.3456, "12.3456"],
    [6, 12.3456, "12.345600"],
  ])("legacy: decimals=%s formatea value numérico %s como %s (Number.toFixed)", (decimals, value, expected) => {
    const { input } = renderInput({ value, decimals, onChange: () => {} });
    expect(input.value).toBe(expected);
  });

  it("legacy: defaultValue numérico se formatea con Number.toFixed(decimals)", () => {
    const { input } = renderInput({ defaultValue: 7.5, decimals: 4 });
    expect(input.value).toBe("7.5000");
  });

  it("contract: value/defaultValue string se pasan sin formatear", () => {
    const { input } = renderInput({ value: "7.5", decimals: 4, onChange: () => {} });
    expect(input.value).toBe("7.5");
    cleanup();
    const { input: def } = renderInput({ defaultValue: "3", decimals: 4 });
    expect(def.value).toBe("3");
  });

  it("legacy: value=0 se muestra como '0.00'", () => {
    const { input } = renderInput({ value: 0, onChange: () => {} });
    expect(input.value).toBe("0.00");
  });

  it("legacy: deuda binaria — value 1.005 con 2 decimales muestra '1.00' (formatMoney da '1.01')", () => {
    const { input } = renderInput({ value: 1.005, decimals: 2, onChange: () => {} });
    expect(input.value).toBe("1.00");
    expect(formatMoney(1.005, 2)).toBe("1.01");
  });

  it("legacy: deuda binaria — value 0.075 con 2 decimales muestra '0.07' (formatMoney da '0.08')", () => {
    const { input } = renderInput({ value: 0.075, decimals: 2, onChange: () => {} });
    expect(input.value).toBe("0.07");
    expect(formatMoney(0.075, 2)).toBe("0.08");
  });
});

describe("ZhDecimalInput — teclado", () => {
  it.each(["0", "5", "9", "Backspace", "Delete", "Tab", "Enter", "ArrowLeft", "ArrowRight", "Home", "End"])(
    "contract: permite la tecla %s",
    (key) => {
      const { input } = renderInput();
      expect(keyAllowed(input, key)).toBe(true);
    },
  );

  it.each(["a", "e", "E", "+", " ", "$"])("contract: bloquea el carácter inválido %s", (key) => {
    const { input } = renderInput();
    expect(keyAllowed(input, key)).toBe(false);
  });

  it("contract: permite atajos con Ctrl/Meta (p. ej. Ctrl+V)", () => {
    const { input } = renderInput();
    expect(keyAllowed(input, "v", { ctrlKey: true })).toBe(true);
    expect(keyAllowed(input, "v", { metaKey: true })).toBe(true);
  });

  it("contract: permite un solo punto decimal", () => {
    const { input } = renderInput();
    expect(keyAllowed(input, ".")).toBe(true);
    typeRaw(input, "1.2");
    expect(keyAllowed(input, ".")).toBe(false);
  });

  it("legacy: la coma se permite como tecla si aún no hay punto (no se convierte a punto)", () => {
    const { input } = renderInput();
    expect(keyAllowed(input, ",")).toBe(true);
  });

  it("contract: decimals=0 bloquea punto y coma", () => {
    const { input } = renderInput({ decimals: 0 });
    expect(keyAllowed(input, ".")).toBe(false);
    expect(keyAllowed(input, ",")).toBe(false);
    typeRaw(input, "123");
    expect(keyAllowed(input, "4")).toBe(true);
  });

  it("contract: bloquea dígitos que excederían decimals tras el punto", () => {
    const { input } = renderInput({ decimals: 2 });
    typeRaw(input, "1.2");
    expect(keyAllowed(input, "3")).toBe(true);
    typeRaw(input, "1.23");
    expect(keyAllowed(input, "4")).toBe(false);
  });

  it("contract: con decimales completos permite dígitos en la parte entera (cursor antes del punto)", () => {
    const { input } = renderInput({ decimals: 2 });
    typeRaw(input, "1.23", 1);
    expect(keyAllowed(input, "9")).toBe(true);
  });

  it("contract: con decimales completos permite reemplazar una selección", () => {
    const { input } = renderInput({ decimals: 2 });
    typeRaw(input, "1.23");
    input.setSelectionRange(2, 4);
    expect(keyAllowed(input, "9")).toBe(true);
  });

  it("contract: '-' solo al inicio y una vez cuando positiveOnly=false", () => {
    const { input } = renderInput();
    expect(keyAllowed(input, "-")).toBe(true);
    typeRaw(input, "5", 1);
    expect(keyAllowed(input, "-")).toBe(false);
    typeRaw(input, "-5", 0);
    expect(keyAllowed(input, "-")).toBe(false);
  });

  it("contract: positiveOnly bloquea '-'", () => {
    const { input } = renderInput({ positiveOnly: true });
    expect(keyAllowed(input, "-")).toBe(false);
  });

  it("contract: invoca onKeyDown del consumidor aun cuando bloquea la tecla", () => {
    const onKeyDown = vi.fn();
    const { input } = renderInput({ onKeyDown });
    keyAllowed(input, "a");
    keyAllowed(input, "1");
    expect(onKeyDown).toHaveBeenCalledTimes(2);
  });
});

describe("ZhDecimalInput — paste", () => {
  it("contract: pega un valor válido y dispara onChange programático", () => {
    const onChange = vi.fn();
    const { input } = renderInput({ onChange });
    expect(paste(input, "12.34")).toBe(false); // preventDefault del paste nativo
    expect(input.value).toBe("12.34");
    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it("legacy: el exceso de decimales en paste se TRUNCA, no se redondea", () => {
    const { input } = renderInput({ decimals: 2 });
    paste(input, "12.3499");
    expect(input.value).toBe("12.34");
  });

  it("contract: elimina caracteres inválidos del texto pegado", () => {
    const { input } = renderInput({ decimals: 2 });
    paste(input, "$ 1,234.56 USD");
    expect(input.value).toBe("1234.56");
  });

  it("contract: paste reemplaza el valor completo (no inserta en el cursor)", () => {
    const { input } = renderInput();
    typeRaw(input, "9", 1);
    paste(input, "12");
    expect(input.value).toBe("12");
  });

  it("contract: paste negativo se conserva sin positiveOnly y se descarta el signo con positiveOnly", () => {
    const { input } = renderInput();
    paste(input, "-5.5");
    expect(input.value).toBe("-5.5");
    cleanup();
    const { input: pos } = renderInput({ positiveOnly: true });
    paste(pos, "-5.5");
    expect(pos.value).toBe("5.5");
  });

  it("contract: paste sin dígitos no cambia el valor ni dispara onChange", () => {
    const onChange = vi.fn();
    const { input } = renderInput({ onChange });
    typeRaw(input, "7");
    onChange.mockClear();
    paste(input, "abc");
    expect(input.value).toBe("7");
    expect(onChange).not.toHaveBeenCalled();
  });

  it("contract: invoca onPaste del consumidor", () => {
    const onPaste = vi.fn();
    const { input } = renderInput({ onPaste });
    paste(input, "1");
    expect(onPaste).toHaveBeenCalledTimes(1);
  });
});

describe("ZhDecimalInput — focus / blur / onChange", () => {
  it("contract: focus no modifica el valor ni dispara onChange", () => {
    const onChange = vi.fn();
    const onFocus = vi.fn();
    const { input } = renderInput({ defaultValue: "5", onChange, onFocus });
    fireEvent.focus(input);
    expect(input.value).toBe("5");
    expect(onChange).not.toHaveBeenCalled();
    expect(onFocus).toHaveBeenCalledTimes(1);
  });

  it("contract: blur sin cambio de formato no dispara onChange (valor ya formateado)", () => {
    const onChange = vi.fn();
    const onBlur = vi.fn();
    const { input } = renderInput({ defaultValue: "5.00", onChange, onBlur });
    fireEvent.blur(input);
    expect(input.value).toBe("5.00");
    expect(onChange).not.toHaveBeenCalled();
    expect(onBlur).toHaveBeenCalledTimes(1);
  });

  it("contract: blur con input vacío no formatea ni dispara onChange", () => {
    const onChange = vi.fn();
    const { input } = renderInput({ onChange });
    fireEvent.blur(input);
    expect(input.value).toBe("");
    expect(onChange).not.toHaveBeenCalled();
  });

  it("legacy: formats numeric value using Number.toFixed on blur y dispara onChange programático", () => {
    const onChange = vi.fn();
    const { input } = renderInput({ decimals: 4, onChange });
    typeRaw(input, "5");
    onChange.mockClear();
    fireEvent.blur(input);
    expect(input.value).toBe("5.0000");
    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it("legacy: blur completa decimales faltantes ('12.2' → '12.200000' con decimals=6)", () => {
    const { input } = renderInput({ decimals: 6 });
    typeRaw(input, "12.2");
    fireEvent.blur(input);
    expect(input.value).toBe("12.200000");
  });

  it("legacy: deuda binaria en blur — '1.005' con decimals=2 queda '1.00'", () => {
    const { input } = renderInput({ decimals: 2 });
    typeRaw(input, "1.005");
    fireEvent.blur(input);
    expect(input.value).toBe("1.00");
  });

  it("legacy: blur usa parseFloat — '12abc' se normaliza a '12.00'", () => {
    const { input } = renderInput();
    typeRaw(input, "12abc");
    fireEvent.blur(input);
    expect(input.value).toBe("12.00");
  });

  it("contract: blur con texto no numérico lo deja intacto", () => {
    const onChange = vi.fn();
    const { input } = renderInput({ onChange });
    typeRaw(input, "abc");
    onChange.mockClear();
    fireEvent.blur(input);
    expect(input.value).toBe("abc");
    expect(onChange).not.toHaveBeenCalled();
  });

  it("legacy: blur con positiveOnly convierte un negativo en '0.00'", () => {
    const { input } = renderInput({ positiveOnly: true });
    typeRaw(input, "-3");
    fireEvent.blur(input);
    expect(input.value).toBe("0.00");
  });

  it("legacy: blur conserva negativos sin positiveOnly", () => {
    const { input } = renderInput();
    typeRaw(input, "-3.5");
    fireEvent.blur(input);
    expect(input.value).toBe("-3.50");
  });

  it("legacy: blur también reformatea un input readOnly", () => {
    const { input } = renderInput({ readOnly: true, defaultValue: "5" });
    fireEvent.blur(input);
    expect(input.value).toBe("5.00");
  });

  it("contract: onChange se dispara en cada edición del usuario", () => {
    const onChange = vi.fn();
    const { input } = renderInput({ onChange });
    fireEvent.change(input, { target: { value: "1" } });
    fireEvent.change(input, { target: { value: "1.5" } });
    expect(onChange).toHaveBeenCalledTimes(2);
  });
});

/**
 * ZH-DESIGN-SYSTEM-PRECISION-03B — API semántica `precision`. Solo resuelve CUÁNTOS decimales
 * (vía SemanticDecimals → usePrecisionDecimals); el comportamiento legacy del input no cambia.
 */
describe("ZhDecimalInput — precision semántica (03B)", () => {
  const POLICY_A: PrecisionPolicy = {
    ...TEST_PRECISION_POLICY,
    quantityDecimals: 3,
    percentageDecimals: 1,
    unitCostDecimals: 5,
    moneyDecimals: 2,
  };
  const POLICY_B: PrecisionPolicy = { ...POLICY_A, quantityDecimals: 1 };

  it.each([
    ["quantity", "1.235"],
    ["percentage", "1.2"],
    ["unitCost", "1.23457"],
  ] as const)("precision=%s usa la escala de la policy (defaultValue 1.234567 → %s)", (precision, expected) => {
    setPrecisionPolicyForTests(POLICY_A);
    const { input } = renderInput({ precision, defaultValue: 1.234567 });
    expect(input.value).toBe(expected);
  });

  it("policy A → B sin remount: value controlado y límite de teclado siguen la nueva escala", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { input } = renderInput({ precision: "quantity", value: 12.3456, onChange: () => {} });
    expect(input.value).toBe("12.346");
    act(() => setPrecisionPolicyForTests(POLICY_B));
    const again = document.querySelector("input");
    expect(again).toBe(input);
    expect(input.value).toBe("12.3");
    typeRaw(input, "1.2");
    expect(keyAllowed(input, "3")).toBe(false); // quantity=1 ya no admite un 2.º decimal
  });

  it("decimals explícito gana sobre precision", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { input } = renderInput({ precision: "quantity", decimals: 4, defaultValue: 1.5 });
    expect(input.value).toBe("1.5000");
  });

  it("decimals={0} es override válido sobre precision", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { input } = renderInput({ precision: "unitCost", decimals: 0, defaultValue: 7.6 });
    expect(input.value).toBe("8");
    expect(keyAllowed(input, ".")).toBe(false);
  });

  it("sin precision ni decimals: legacy 2 SIN policy cargada (no impacto en los 60 consumidores)", () => {
    setPrecisionPolicyForTests(null);
    const { input } = renderInput({ defaultValue: 5 });
    expect(input.value).toBe("5.00");
    cleanup();
    const legacy = renderInput({ value: "5", onChange: () => {} });
    expect(legacy.input.value).toBe("5");
  });

  it("forwardRef expone el HTMLInputElement también con precision", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const ref = React.createRef<HTMLInputElement>();
    render(<ZhDecimalInput ref={ref} aria-label="valor" precision="quantity" />);
    expect(ref.current).toBeInstanceOf(HTMLInputElement);
    expect(ref.current?.className).toBe("zh-numeric-input");
  });

  it("value string controlado y defaultValue string se pasan sin formatear, igual que legacy", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { input } = renderInput({ precision: "quantity", value: "7.5", onChange: () => {} });
    expect(input.value).toBe("7.5");
    cleanup();
    const def = renderInput({ precision: "quantity", defaultValue: "3" });
    expect(def.input.value).toBe("3");
  });

  it("precision no cambia blur/onChange: formatea con la escala resuelta y emite una vez; ya formateado no emite", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const onChange = vi.fn();
    const { input } = renderInput({ precision: "quantity", onChange });
    typeRaw(input, "5");
    onChange.mockClear();
    fireEvent.blur(input);
    expect(input.value).toBe("5.000");
    expect(onChange).toHaveBeenCalledTimes(1);
    onChange.mockClear();
    fireEvent.blur(input);
    expect(onChange).not.toHaveBeenCalled();
  });

  it("precision no cambia el motor: la deuda binaria legacy sigue (money=2, 1.005 → '1.00')", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { input } = renderInput({ precision: "money", value: 1.005, onChange: () => {} });
    expect(input.value).toBe("1.00");
  });

  it("precision no cambia paste: sigue truncando a la escala resuelta", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { input } = renderInput({ precision: "quantity" });
    paste(input, "12.34567");
    expect(input.value).toBe("12.345");
  });

  it("RHF register funciona con precision (ref + onChange + blur)", () => {
    setPrecisionPolicyForTests(POLICY_A);
    let getValues: () => { qty: string } = () => ({ qty: "" });
    function Form() {
      const form = useForm<{ qty: string }>({ defaultValues: { qty: "" } });
      getValues = form.getValues;
      return <ZhDecimalInput aria-label="valor" precision="quantity" {...form.register("qty")} />;
    }
    const { getByLabelText } = render(<Form />);
    const input = getByLabelText("valor") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "2.5" } });
    expect(getValues().qty).toBe("2.5");
    fireEvent.blur(input);
    expect(input.value).toBe("2.500");
    expect(getValues().qty).toBe("2.500");
  });
});
