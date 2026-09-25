// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { SalesInvoiceLineGridRow } from "./SalesInvoiceLineGridRow";
import { setPrecisionPolicyForTests, type PrecisionPolicy } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";
import type { WarehouseDto } from "../../inventory/types";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-03E — piloto en Ventas: Dto. % → `precision="percentage"`, precio
 * facturado → `precision="salesUnitPrice"`, cantidad → `precision="quantity"`. Los callbacks
 * (onUpdate) y el contrato de invoicedUnitPrice no cambian; la escala sale del Design System.
 */

afterEach(() => cleanup());

const WAREHOUSES: WarehouseDto[] = [{ id: "wh-1", name: "Bodega" } as WarehouseDto];
const POLICY: PrecisionPolicy = {
  ...TEST_PRECISION_POLICY,
  quantityDecimals: 4,
  percentageDecimals: 3,
  salesUnitPriceDecimals: 4,
};

function renderRow(overrides: Partial<SalesLineFormValues> = {}) {
  const onUpdate = vi.fn();
  const line: SalesLineFormValues = {
    _key: 1,
    itemId: "item-1",
    warehouseId: "wh-1",
    description: "MANJAR",
    quantity: 12.5,
    unitPrice: 0.3,
    vatCode: "10",
    discountPct: 10,
    _sku: "M1",
    _name: "MANJAR",
    _pvp: 0.3,
    _basePrice: 0.3,
    _isManualPrice: false,
    ...overrides,
  };
  const { container } = render(
    <MemoryRouter>
      <SalesInvoiceLineGridRow
        line={line}
        readOnly={false}
        disabled={false}
        index={0}
        vatLabel="IVA 15%"
        vatRates={{ "10": 15 }}
        warehouses={WAREHOUSES}
        selectedWarehouseId="wh-1"
        onUpdate={onUpdate}
        onUpdateWarehouse={() => {}}
        onRemove={() => {}}
      />
    </MemoryRouter>,
  );
  const q = (sel: string) => container.querySelector<HTMLInputElement>(sel)!;
  return {
    onUpdate,
    container,
    qty: () => q(".sf-product__qty-input"),
    discount: () => q(".sf-product__disc-input"),
    price: () => q(".sf-product__price-input"),
  };
}

describe("SalesInvoiceLineGridRow — precisión semántica en inputs (03E)", () => {
  it("cada input toma su escala semántica de la policy (quantity 4, percentage 3, salesUnitPrice 4)", () => {
    setPrecisionPolicyForTests(POLICY);
    const row = renderRow();
    expect(row.qty().value).toBe("12.5000");
    expect(row.discount().value).toBe("10.000");
    expect(row.price().value).toBe("0.2700"); // 0.3 − 10 %
  });

  it("policy A → B sin remount: cantidad limita el teclado y normaliza la próxima edición con quantity=2", () => {
    setPrecisionPolicyForTests(POLICY);
    const row = renderRow();
    const qty = row.qty();
    act(() => setPrecisionPolicyForTests({ ...POLICY, quantityDecimals: 2 }));
    expect(row.qty()).toBe(qty);
    fireEvent.focus(qty);
    fireEvent.change(qty, { target: { value: "3.45" } });
    qty.setSelectionRange(4, 4);
    expect(fireEvent.keyDown(qty, { key: "6" })).toBe(false);
    fireEvent.change(qty, { target: { value: "3.4" } });
    fireEvent.blur(qty);
    expect(qty.value).toBe("3.40");
    expect(row.onUpdate).toHaveBeenLastCalledWith(1, "quantity", 3.4);
  });

  it("edición real de cantidad: 12.3456 → onUpdate('quantity', 12.3456), contrato sin cambios", () => {
    setPrecisionPolicyForTests(POLICY);
    const row = renderRow();
    fireEvent.focus(row.qty());
    fireEvent.change(row.qty(), { target: { value: "12.3456" } });
    fireEvent.blur(row.qty());
    expect(row.onUpdate).toHaveBeenLastCalledWith(1, "quantity", 12.3456);
  });

  it("precio facturado conserva su contrato: editar emite 'invoicedUnitPrice'; foco/blur sin editar NO llama onUpdate (guarda propia)", () => {
    setPrecisionPolicyForTests(POLICY);
    const row = renderRow();
    fireEvent.focus(row.price());
    fireEvent.blur(row.price());
    expect(row.onUpdate).not.toHaveBeenCalled();
    fireEvent.focus(row.price());
    fireEvent.change(row.price(), { target: { value: "0.25" } });
    fireEvent.blur(row.price());
    expect(row.onUpdate).toHaveBeenCalledWith(1, "invoicedUnitPrice", 0.25);
  });

  it("cantidad y Dto. %: foco/blur sin editar no reescriben el input, pero el onBlur del consumidor CONFIRMA el mismo valor", () => {
    setPrecisionPolicyForTests(POLICY);
    const row = renderRow();
    fireEvent.focus(row.qty());
    fireEvent.blur(row.qty());
    fireEvent.focus(row.discount());
    fireEvent.blur(row.discount());
    expect(row.qty().value).toBe("12.5000");
    expect(row.discount().value).toBe("10.000");
    // Comportamiento real del consumidor (sin guarda): recibe los mismos valores de la línea.
    expect(row.onUpdate).toHaveBeenCalledWith(1, "quantity", 12.5);
    expect(row.onUpdate).toHaveBeenCalledWith(1, "discountPct", 10);
  });

  it("no altera el cálculo fiscal mostrado (Base/IVA/Total)", () => {
    setPrecisionPolicyForTests(POLICY);
    const row = renderRow({ quantity: 2, discountPct: 0 });
    const fiscal = [...row.container.querySelectorAll(".sf-product__subtotal-value, .sf-product__total-amount")].map(
      (el) => el.textContent,
    );
    expect(fiscal).toEqual(["$0.60", "$0.09", "$0.69"]);
  });
});
