// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { AdjustmentLineCard } from "./AdjustmentLineCard";
import { setPrecisionPolicyForTests } from "../../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../../test/precisionPolicyFixture";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-03E — piloto: la cantidad declara `precision="quantity"` y el costo
 * unitario base `precision="unitCost"`; la escala sale de la PrecisionPolicy vía el Design System.
 * Callbacks (onPatch) y payload sin cambios.
 */

afterEach(() => cleanup());

type CardProps = Parameters<typeof AdjustmentLineCard>[0];

function renderCard(overrides: { quantity?: number; unitCostBase?: number | null } = {}) {
  const onPatch = vi.fn();
  const view = {
    line: {
      _key: 1,
      itemName: "Arroz",
      sku: "ARZ",
      quantity: overrides.quantity ?? 12.5,
      unitCostBase: overrides.unitCostBase ?? 0.2261,
      packagingLevelId: null,
      packagingLevels: [],
      baseUomCode: "UND",
      currentStock: 10,
      lineNotes: "",
    },
    uomCode: "UND",
    quantityInBaseUom: overrides.quantity ?? 12.5,
    insufficientStock: false,
  } as unknown as CardProps["view"];
  render(
    <I18nProvider>
      <AdjustmentLineCard
        index={0}
        view={view}
        movementType="Ingreso"
        formLocked={false}
        onPatch={onPatch}
        onRemove={() => {}}
      />
    </I18nProvider>,
  );
  const quantity = screen.getByLabelText(/Cantidad Arroz/) as HTMLInputElement;
  const cost = screen.getByLabelText(/Costo unitario base Arroz/) as HTMLInputElement;
  return { onPatch, quantity, cost };
}

describe("AdjustmentLineCard — precisión semántica en inputs (03E)", () => {
  it("cantidad usa quantityDecimals y costo base usa unitCostDecimals de la policy", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 4, unitCostDecimals: 6 });
    const { quantity, cost } = renderCard();
    expect(quantity.value).toBe("12.5000");
    expect(cost.value).toBe("0.226100");
  });

  it("policy A → B sin remount: el límite de teclado y el blur tras editar siguen la nueva escala", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 4, unitCostDecimals: 6 });
    const { quantity } = renderCard();
    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 2, unitCostDecimals: 4 }));
    const again = screen.getByLabelText(/Cantidad Arroz/);
    expect(again).toBe(quantity);
    // Campo NO controlado y sin editar: conserva el texto montado (no se reescribe sin edición);
    // la escala B gobierna el teclado y el blur de la próxima edición.
    expect(quantity.value).toBe("12.5000");
    fireEvent.change(quantity, { target: { value: "3.45" } });
    quantity.setSelectionRange(4, 4);
    expect(fireEvent.keyDown(quantity, { key: "6" })).toBe(false); // quantity=2: no admite 3.er decimal
    fireEvent.blur(quantity);
    expect(quantity.value).toBe("3.45");
  });

  it("edición real → onPatch con el mismo valor lógico que antes (cantidad y costo)", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 4, unitCostDecimals: 6 });
    const { quantity, cost, onPatch } = renderCard();
    fireEvent.focus(quantity);
    fireEvent.change(quantity, { target: { value: "7.25" } });
    fireEvent.blur(quantity);
    expect(quantity.value).toBe("7.2500");
    expect(onPatch).toHaveBeenLastCalledWith(1, { quantity: 7.25 });
    fireEvent.focus(cost);
    fireEvent.change(cost, { target: { value: "0.3" } });
    fireEvent.blur(cost);
    expect(onPatch).toHaveBeenLastCalledWith(1, { unitCostBase: 0.3 });
  });

  it("focus → blur sin editar: el input no reescribe, pero el onBlur del consumidor CONFIRMA igual (mismo valor)", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 4, unitCostDecimals: 6 });
    const { quantity, onPatch } = renderCard();
    fireEvent.focus(quantity);
    fireEvent.blur(quantity);
    expect(quantity.value).toBe("12.5000");
    // Comportamiento real del consumidor (sin guarda propia): onPatch se llama con el valor sin cambios.
    expect(onPatch).toHaveBeenCalledWith(1, { quantity: 12.5 });
  });

  it("hereda la coma del Design System: '1,5' → onPatch quantity 1.5", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 4 });
    const { quantity, onPatch } = renderCard();
    fireEvent.focus(quantity);
    fireEvent.paste(quantity, { clipboardData: { getData: () => "1,5" } });
    fireEvent.blur(quantity);
    expect(quantity.value).toBe("1.5000");
    expect(onPatch).toHaveBeenLastCalledWith(1, { quantity: 1.5 });
  });
});
