// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { useState } from "react";
import { act, render, cleanup, fireEvent } from "@testing-library/react";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import { ReturnableLinesEditor } from "./ReturnableLinesEditor";
import type { ReturnableLineDto } from "../api/salesReturnService";

// SALES-DS-MONEY-12 — la columna "P. unitario" migró de formatMoney a
// ZHMoneyValue (decimals=dc.salesUnitPrice, sin símbolo de moneda — nunca
// mostró "$"). La cantidad a devolver sigue siendo un input editable.

function buildLine(overrides: Partial<ReturnableLineDto> = {}): ReturnableLineDto {
  return {
    invoiceDetailId: "od-1",
    itemId: "item-1",
    description: "Producto X",
    snapshotSku: "SKU-1",
    warehouseId: "wh-1",
    uomCode: "UND",
    originalQuantity: 5,
    returnedQuantity: 0,
    remainingQuantity: 5,
    unitPrice: 12.5,
    discountPct: 0,
    vatCode: "10",
    vatRate: 15,
    iceCode: null,
    iceRate: 0,
    packagingLevelId: null,
    conversionFactor: 1,
    originalQuantityInBaseUom: 5,
    remainingQuantityInBaseUom: 5,
    baseUomCode: "UND",
    ...overrides,
  };
}

afterEach(() => {
  cleanup();
});

describe("ReturnableLinesEditor — P. unitario migrado a ZHMoneyValue (SALES-DS-MONEY-12)", () => {
  it("la columna P. unitario usa ZHMoneyValue sin símbolo de moneda", () => {
    const { container } = render(
      <ReturnableLinesEditor
        lines={[buildLine({ unitPrice: 12.5 })]}
        quantities={{}}
        onChangeQuantity={vi.fn()}
      />,
    );

    const cell = container.querySelector(
      "td.zh-table-cell--num .zh-money-value",
    );
    expect(cell).toBeTruthy();
    expect(cell?.textContent).toBe("12.50");
  });

  it("la cantidad a devolver sigue siendo un input editable, no ZHMoneyValue", () => {
    const { container } = render(
      <ReturnableLinesEditor
        lines={[buildLine()]}
        quantities={{ "od-1": "2" }}
        onChangeQuantity={vi.fn()}
      />,
    );

    const input = container.querySelector("input");
    expect(input).toBeTruthy();
    expect(input?.value).toBe("2");
  });

  it("no hay estilos inline en el valor migrado", () => {
    const { container } = render(
      <ReturnableLinesEditor
        lines={[buildLine()]}
        quantities={{}}
        onChangeQuantity={vi.fn()}
      />,
    );

    container.querySelectorAll(".zh-money-value").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});

// SALES-PRESENTATIONS-04 — la devolución muestra las cantidades en la MISMA presentación
// vendida (nunca un selector para cambiarla), con la equivalencia en unidad base como dato
// secundario cuando el factor de conversión no es 1.
describe("ReturnableLinesEditor — presentación (SALES-PRESENTATIONS-04)", () => {
  it("vendido 2 cajas x12, devuelto 1, pendiente 1: muestra unidad y equivalencia base", () => {
    const { container } = render(
      <ReturnableLinesEditor
        lines={[
          buildLine({
            uomCode: "CAJA",
            originalQuantity: 2,
            returnedQuantity: 1,
            remainingQuantity: 1,
            conversionFactor: 12,
            originalQuantityInBaseUom: 24,
            remainingQuantityInBaseUom: 12,
            baseUomCode: "UNIT",
          }),
        ]}
        quantities={{}}
        onChangeQuantity={vi.fn()}
      />,
    );

    expect(container.textContent).toContain("2 CAJA");
    expect(container.textContent).toContain("= 24 UNIT");
    expect(container.textContent).toContain("1 CAJA");
    expect(container.textContent).toContain("= 12 UNIT");
  });

  it("sin presentación (factor 1): no muestra equivalencia (comportamiento actual)", () => {
    const { container } = render(
      <ReturnableLinesEditor
        lines={[buildLine({ conversionFactor: 1 })]}
        quantities={{}}
        onChangeQuantity={vi.fn()}
      />,
    );

    expect(container.querySelector(".sr-lines-table__equivalence")).toBeNull();
  });

  it("no permite seleccionar una presentación distinta a la vendida (sin selector)", () => {
    const { container } = render(
      <ReturnableLinesEditor
        lines={[buildLine({ uomCode: "CAJA", conversionFactor: 12 })]}
        quantities={{}}
        onChangeQuantity={vi.fn()}
      />,
    );

    expect(container.querySelector("select")).toBeNull();
  });
});

/** ZH-DESIGN-SYSTEM-PRECISION-04D — "cantidad a devolver" declara `precision="quantity"`. */
describe("ReturnableLinesEditor — precision='quantity' (04D)", () => {
  function Stateful({ spy }: { spy: (id: string, v: string) => void }) {
    const [quantities, setQuantities] = useState<Record<string, string>>({});
    return (
      <ReturnableLinesEditor
        lines={[buildLine()]}
        quantities={quantities}
        onChangeQuantity={(id, v) => {
          spy(id, v);
          setQuantities((q) => ({ ...q, [id]: v }));
        }}
      />
    );
  }
  function allowsDecimal(input: HTMLInputElement, digits: number) {
    fireEvent.change(input, { target: { value: `1.${"1".repeat(digits)}` } });
    input.setSelectionRange(input.value.length, input.value.length);
    return fireEvent.keyDown(input, { key: "9" });
  }

  it("usa quantityDecimals de la policy y reacciona A → B sin remount", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 3 });
    const { container } = render(<Stateful spy={() => {}} />);
    const qty = container.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
    expect([allowsDecimal(qty, 2), allowsDecimal(qty, 3)]).toEqual([true, false]);
    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 1 }));
    expect(container.querySelector("input.zh-numeric-input")).toBe(qty);
    expect(allowsDecimal(qty, 1)).toBe(false);
  });

  it("paste '2,5' → onChangeQuantity recibe el texto canónico '2.5' (mismo contrato)", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 4 });
    const spy = vi.fn();
    const { container } = render(<Stateful spy={spy} />);
    const qty = container.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
    fireEvent.paste(qty, { clipboardData: { getData: () => "2,5" } });
    expect(spy).toHaveBeenLastCalledWith("od-1", "2.5");
  });
});

/** Lote 2B — migración a ZHDataTable: estructura, foco, decimales intermedios, exceso y accesibilidad. */
describe("ReturnableLinesEditor — ZHDataTable (Lote 2B)", () => {
  function Stateful({ initial = {}, disabled }: { initial?: Record<string, string>; disabled?: boolean }) {
    const [quantities, setQuantities] = useState<Record<string, string>>(initial);
    return (
      <>
        <ReturnableLinesEditor
          lines={[buildLine()]}
          quantities={quantities}
          disabled={disabled}
          onChangeQuantity={(id, v) => setQuantities((q) => ({ ...q, [id]: v }))}
        />
        <output data-testid="raw">{quantities["od-1"] ?? ""}</output>
      </>
    );
  }

  it("renderiza ZHDataTable con las seis columnas y las clases de tabla esperadas", () => {
    const { container } = render(<Stateful />);
    const table = container.querySelector("table");
    expect(table?.className).toBe("table table--compact table--neutral sr-lines-table");
    expect(container.querySelector(".table-scroll")).toBeTruthy();
    expect(Array.from(container.querySelectorAll("th")).map((th) => th.textContent)).toEqual([
      "Producto",
      "Cant. original",
      "Ya devuelto",
      "Remanente",
      "P. unitario",
      "Cant. a devolver",
    ]);
  });

  it("conserva el foco del input mientras se escribe", () => {
    const { container } = render(<Stateful />);
    const input = container.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
    input.focus();
    fireEvent.change(input, { target: { value: "2" } });
    expect(container.querySelector("input.zh-numeric-input")).toBe(input);
    expect(document.activeElement).toBe(input);
  });

  it("no pierde el texto intermedio \"3.\" ni \"3.50\"", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 3 });
    const { container, getByTestId } = render(<Stateful initial={{ "od-1": "" }} />);
    const input = container.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
    fireEvent.change(input, { target: { value: "3." } });
    expect(getByTestId("raw").textContent).toBe("3.");
    expect(input.value).toBe("3.");
    fireEvent.change(input, { target: { value: "3.50" } });
    expect(getByTestId("raw").textContent).toBe("3.50");
    expect(input.value).toBe("3.50");
  });

  it("expone aria-label descriptivo y aria-invalid=false dentro del remanente", () => {
    const { getByLabelText } = render(<Stateful initial={{ "od-1": "2" }} />);
    const input = getByLabelText("Cantidad a devolver: Producto X");
    expect(input.getAttribute("aria-invalid")).toBe("false");
    expect(input.className).not.toContain("sr-qty-input--error");
  });

  it("marca exceso con aria-invalid, clase visual y mensaje", () => {
    const { getByLabelText, container } = render(<Stateful initial={{ "od-1": "6" }} />);
    const input = getByLabelText("Cantidad a devolver: Producto X");
    expect(input.getAttribute("aria-invalid")).toBe("true");
    expect(input.className).toContain("sr-qty-input--error");
    expect(container.textContent).toContain("Excede el remanente (5).");
    expect(container.querySelector("[role='alert']")).toBeNull();
  });

  it("deshabilita el input con disabled o sin remanente", () => {
    const a = render(<Stateful disabled />);
    expect(a.container.querySelector<HTMLInputElement>("input")!.disabled).toBe(true);
    a.unmount();
    const b = render(
      <ReturnableLinesEditor
        lines={[buildLine({ remainingQuantity: 0 })]}
        quantities={{}}
        onChangeQuantity={vi.fn()}
      />,
    );
    expect(b.container.querySelector<HTMLInputElement>("input")!.disabled).toBe(true);
  });

  it("sin líneas conserva el estado vacío previo a la tabla", () => {
    const { container } = render(
      <ReturnableLinesEditor lines={[]} quantities={{}} onChangeQuantity={vi.fn()} />,
    );
    expect(container.querySelector(".zh-inline-empty")?.textContent).toBe(
      "Esta factura no tiene líneas devolvibles.",
    );
    expect(container.querySelector("table")).toBeNull();
  });
});
