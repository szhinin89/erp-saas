// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { createRef } from "react";
import { cleanup, render } from "@testing-library/react";
import { ZHField } from "./ZHForm";
import {
  ZhCheckbox,
  ZhCurrencyInput,
  ZhDateInput,
  ZhDateTimeInput,
  ZhDecimalInput,
  ZhNumberInput,
  ZhPhoneInput,
  ZhSelect,
  ZhTextInput,
  ZhTextarea,
} from "./inputs";

afterEach(cleanup);

/**
 * Paquete C3 — contrato compartido de error: dentro de un `ZHField` con `fieldError`/`error`, los
 * inputs oficiales reciben `aria-invalid` y `aria-describedby` hacia el mensaje visible. Sin
 * error, o fuera de un ZHField, no cambia nada. Los atributos explícitos del consumidor ganan.
 */
const MESSAGE = "Campo obligatorio";

function control(container: HTMLElement) {
  return container.querySelector("input, select, textarea") as HTMLElement;
}
function messageNode(container: HTMLElement) {
  return container.querySelector("p.zh-field-hint") as HTMLElement;
}

describe("ZHField + inputs oficiales — error accesible", () => {
  const cases: [string, () => React.ReactElement][] = [
    ["ZhTextInput", () => <ZhTextInput />],
    ["ZhSelect", () => <ZhSelect><option value="">—</option></ZhSelect>],
    ["ZhTextarea", () => <ZhTextarea />],
    ["ZhNumberInput", () => <ZhNumberInput />],
    ["ZhDecimalInput", () => <ZhDecimalInput precision="money" />],
    ["ZhCurrencyInput", () => <ZhCurrencyInput precision="money" />],
    ["ZhDateInput", () => <ZhDateInput />],
    ["ZhDateTimeInput", () => <ZhDateTimeInput />],
    ["ZhPhoneInput", () => <ZhPhoneInput />],
    ["ZhCheckbox", () => <ZhCheckbox />],
  ];

  for (const [name, make] of cases) {
    it(`${name}: con fieldError recibe aria-invalid y aria-describedby → mensaje`, () => {
      const { container } = render(<ZHField label="Campo" fieldError={MESSAGE}>{make()}</ZHField>);
      const el = control(container);
      const msg = messageNode(container);
      expect(msg.textContent).toBe(MESSAGE);
      expect(msg.id).toBeTruthy();
      expect(el.getAttribute("aria-invalid")).toBe("true");
      expect(el.getAttribute("aria-describedby")).toBe(msg.id);
    });

    it(`${name}: sin error (o fuera de ZHField) no añade atributos`, () => {
      const inside = render(<ZHField label="Campo" hint="Ayuda">{make()}</ZHField>);
      expect(control(inside.container).hasAttribute("aria-invalid")).toBe(false);
      expect(control(inside.container).hasAttribute("aria-describedby")).toBe(false);
      expect(messageNode(inside.container).id).toBe("");
      inside.unmount();
      const outside = render(make());
      expect(control(outside.container).hasAttribute("aria-invalid")).toBe(false);
      expect(control(outside.container).hasAttribute("aria-describedby")).toBe(false);
    });
  }

  it("alias `error` de ZHField también publica el contrato", () => {
    const { container } = render(<ZHField label="Campo" error={MESSAGE}><ZhTextInput /></ZHField>);
    expect(control(container).getAttribute("aria-invalid")).toBe("true");
  });

  it("los atributos explícitos del consumidor ganan y su aria-describedby se conserva", () => {
    const { container } = render(
      <ZHField label="Campo" fieldError={MESSAGE}>
        <ZhTextInput aria-invalid={false} aria-describedby="ayuda-propia" />
      </ZHField>,
    );
    const el = control(container);
    expect(el.getAttribute("aria-invalid")).toBe("false");
    expect(el.getAttribute("aria-describedby")).toBe(`ayuda-propia ${messageNode(container).id}`);
  });

  it("un aria-describedby explícito se respeta sin error", () => {
    const { container } = render(<ZHField label="Campo"><ZhSelect aria-describedby="x"><option /></ZhSelect></ZHField>);
    expect(control(container).getAttribute("aria-describedby")).toBe("x");
  });

  it("dos campos con error no comparten id de mensaje", () => {
    const { container } = render(
      <>
        <ZHField label="A" fieldError="Error A"><ZhTextInput /></ZHField>
        <ZHField label="B" fieldError="Error B"><ZhTextInput /></ZHField>
      </>,
    );
    const ids = [...container.querySelectorAll("p.zh-field-hint")].map((p) => p.id);
    expect(ids).toHaveLength(2);
    expect(new Set(ids).size).toBe(2);
    const inputs = [...container.querySelectorAll("input")];
    expect(inputs.map((i) => i.getAttribute("aria-describedby"))).toEqual(ids);
  });

  it("la presentación no cambia: clase de error y texto del mensaje siguen igual", () => {
    const { container } = render(<ZHField label="Campo" fieldError={MESSAGE}><ZhTextInput /></ZHField>);
    expect(container.querySelector("label.zh-field")!.className).toContain("zh-field--error");
    expect(messageNode(container).className).toContain("zh-field-hint--error");
  });

  it("forwardRef sigue entregando el elemento nativo", () => {
    const text = createRef<HTMLInputElement>();
    const sel = createRef<HTMLSelectElement>();
    const area = createRef<HTMLTextAreaElement>();
    const chk = createRef<HTMLInputElement>();
    render(
      <ZHField label="Campo" fieldError={MESSAGE}>
        <ZhTextInput ref={text} />
        <ZhSelect ref={sel}><option /></ZhSelect>
        <ZhTextarea ref={area} />
        <ZhCheckbox ref={chk} />
      </ZHField>,
    );
    expect(text.current?.tagName).toBe("INPUT");
    expect(sel.current?.tagName).toBe("SELECT");
    expect(area.current?.tagName).toBe("TEXTAREA");
    expect(chk.current?.type).toBe("checkbox");
  });
});
