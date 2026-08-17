// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { DistributeCostModal } from "./DistributeCostModal";
import type { DistributeCostSourceLine } from "../utils/purchaseCalc";

const lines: DistributeCostSourceLine[] = [
  {
    id: "line-1",
    description: "Producto Uno",
    quantity: 10,
    quantityInBaseUom: 10,
    unitPrice: 5,
    discountAmount: 0,
    landedUnitCost: 5,
    totalLineCost: 50,
  },
  {
    id: "line-2",
    description: "Producto Dos",
    quantity: 5,
    quantityInBaseUom: 5,
    unitPrice: 10,
    discountAmount: 0,
    landedUnitCost: 10,
    totalLineCost: 50,
  },
];

function renderModal(onApply = vi.fn().mockResolvedValue(true)) {
  const utils = render(
    <I18nProvider>
      <DistributeCostModal
        open
        lines={lines}
        totalFreight={0}
        totalOtherCosts={0}
        grandTotal={100}
        onCancel={vi.fn()}
        onApply={onApply}
      />
    </I18nProvider>,
  );
  return { ...utils, onApply };
}

afterEach(() => {
  cleanup();
});

describe("DistributeCostModal — valores monetarios de solo lectura (ZHMoneyValue)", () => {
  it("las celdas de costo/subtotal de la grilla usan ZHMoneyValue sin símbolo de moneda", () => {
    const { container } = renderModal();

    // "Costo unit. actual" / "Subtotal base" de la primera línea: $5.00 y $50.00 sin el "$".
    const cells = container.querySelectorAll(
      "td.zh-table-cell--num .zh-money-value",
    );
    expect(cells.length).toBeGreaterThan(0);
    cells.forEach((el) => {
      expect(el.textContent).not.toMatch(/\$/);
      expect(el.getAttribute("style")).toBeNull();
    });
  });

  it('muestra "—" en las columnas calculadas hasta pulsar Calcular', () => {
    const { container } = renderModal();

    const calculatedCells = container.querySelectorAll(
      "td.zh-table-cell--num .zh-money-value--empty",
    );
    expect(calculatedCells.length).toBeGreaterThan(0);
    calculatedCells.forEach((el) => expect(el.textContent).toBe("—"));
  });

  it("tras digitar un monto y pulsar Calcular, las columnas calculadas muestran el valor asignado", () => {
    const { container } = renderModal();

    const amountInput = container.querySelector(
      ".pdc-toolbar input",
    ) as HTMLInputElement;
    fireEvent.blur(amountInput, { target: { value: "20" } });
    fireEvent.click(screen.getByText("Calcular"));

    const calculatedCells = container.querySelectorAll(
      "td.zh-table-cell--num .zh-money-value--empty",
    );
    expect(calculatedCells.length).toBe(0);
  });

  it("los totales del pie (antes <strong>) migraron a ZHMoneyValue emphasis=strong", () => {
    const { container } = renderModal();

    const totalsRows = container.querySelectorAll(".pdc-totals__row");
    expect(totalsRows.length).toBeGreaterThan(0);
    totalsRows.forEach((row) => {
      expect(row.querySelector("strong")).toBeNull();
      const money = row.querySelector(".zh-money-value--strong");
      expect(money).toBeTruthy();
    });
  });

  it("el checkbox de incluir línea sigue siendo un input nativo (excepción documentada)", () => {
    const { container } = renderModal();

    const checkbox = container.querySelector('input[type="checkbox"]');
    expect(checkbox).toBeTruthy();
  });
});
