// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { DistributeCostModal } from "./DistributeCostModal";
import type { DistributeCostSourceLine } from "../utils/purchaseCalc";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

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

describe("DistributeCostModal — precisión (ERP-PRECISION-FRONTEND-06B)", () => {
  it("cantidad usa quantityDecimals y % participación usa percentageDecimals", () => {
    setPrecisionPolicyForTests({
      ...TEST_PRECISION_POLICY,
      quantityDecimals: 3,
      percentageDecimals: 4,
    });
    const { container } = renderModal();

    const amountInput = container.querySelector(".pdc-toolbar input") as HTMLInputElement;
    fireEvent.blur(amountInput, { target: { value: "20" } });
    fireEvent.click(screen.getByText("Calcular"));

    const cellTexts = Array.from(container.querySelectorAll("tbody td")).map((td) => td.textContent);
    expect(cellTexts).toContain("10.000");
    expect(cellTexts).toContain("5.000");
    // Ambas líneas valen 50 de 100 → 50% cada una, con 4 decimales.
    expect(cellTexts.filter((t) => t === "50.0000%").length).toBe(2);
  });

  it("con quantityDecimals=0 la cantidad no muestra decimales", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 0 });
    const { container } = renderModal();

    const cellTexts = Array.from(container.querySelectorAll("tbody td")).map((td) => td.textContent);
    expect(cellTexts).toContain("10");
    expect(cellTexts).toContain("5");
  });
});

describe("DistributeCostModal — mensajes P2", () => {
  it("el monto vacío es una instrucción en su campo, no un aviso; se conserva la advertencia de transporte", () => {
    renderModal();
    expect(screen.queryByRole("alert")).toBeNull();
    const hint = screen.getByText("El valor a distribuir debe ser mayor a cero.");
    expect(hint.className).toContain("zh-field-hint--muted");
    expect(screen.getByText(/Elimine antes cualquier línea de transporte/)).toBeTruthy();
  });

  it("la selección inválida de ítems se avisa una sola vez sobre la tabla", () => {
    const { container } = renderModal();
    fireEvent.blur(container.querySelector(".pdc-toolbar input") as HTMLInputElement, { target: { value: "20" } });
    container.querySelectorAll('tbody input[type="checkbox"]').forEach((c) => fireEvent.click(c));
    expect(screen.getAllByRole("alert").map((a) => a.textContent)).toEqual([expect.stringContaining("Debe incluir al menos un ítem.")]);
  });

  it("los totales no repiten el mismo valor bajo dos etiquetas", () => {
    const { container } = renderModal();
    const labels = Array.from(container.querySelectorAll(".pdc-totals__row > span:first-child")).map((s) => s.textContent);
    expect(labels).toHaveLength(8);
    expect(labels).toContain("Total factura después / CxP esperada");
    expect(labels).toContain("Diferencia de redondeo / por cuadrar");
    expect(labels).not.toContain("Valor a distribuir");
  });

  it("conserva el rechazo del servidor dentro del modal", () => {
    render(
      <I18nProvider>
        <DistributeCostModal open error="Rechazado por el servidor" lines={lines} totalFreight={0} totalOtherCosts={0}
          grandTotal={100} onCancel={vi.fn()} onApply={vi.fn()} />
      </I18nProvider>,
    );
    expect(screen.getByRole("alert").textContent).toContain("Rechazado por el servidor");
  });
});

describe("DistributeCostModal — diseño compacto (PURCHASES-DISTRIBUTE-COST-UX-01)", () => {
  const longName =
    "CERVEZA CORONA EXTRA 330CC X6U LOCAL MQ — SIXPACK X6 RETORNABLE PROMOCIÓN NAVIDEÑA EDICIÓN LIMITADA";
  const line = (id: string, description: string, quantity: number, unitPrice: number, discountAmount = 0) => {
    const total = Math.round((quantity * unitPrice - discountAmount) * 100) / 100;
    return { id, description, quantity, quantityInBaseUom: quantity, unitPrice, discountAmount,
      landedUnitCost: total / quantity, totalLineCost: total };
  };
  const manyLines: DistributeCostSourceLine[] = [
    line("l1", longName, 180, 1.086945, 16.24),
    line("l2", "AGUA MINERAL GÜITIG 500ML", 48, 0.4275),
    line("l3", "PAPEL HIGIÉNICO SCOTT CUIDADO COMPLETO 4 ROLLOS DOBLE HOJA", 24, 2.3333),
    line("l4", "ACEITE LA FAVORITA 1L", 12, 3.1785, 1.15),
    line("l5", "ARROZ", 100, 0.9123),
    line("l6", "LECHE ENTERA VITA LARGA VIDA 1 LITRO FUNDA", 60, 0.8812),
  ];
  const renderMany = (rows = manyLines) =>
    render(
      <I18nProvider>
        <DistributeCostModal open lines={rows} totalFreight={4.5} totalOtherCosts={0} grandTotal={714.86}
          onCancel={vi.fn()} onApply={vi.fn()} />
      </I18nProvider>,
    );
  const calculate = (container: HTMLElement, value: string) => {
    fireEvent.blur(container.querySelector(".pdc-toolbar input") as HTMLInputElement, { target: { value } });
    fireEvent.click(screen.getByText("Calcular"));
  };
  const summaryValue = (container: HTMLElement, label: string) =>
    Array.from(container.querySelectorAll(".pdc-totals__row"))
      .find((row) => row.querySelector(".zh-summary-grid__label")?.textContent === label)
      ?.querySelector(".zh-summary-grid__value")?.textContent;

  it("la tabla vive en su propia región de scroll horizontal, con la columna Ítem fija", () => {
    const { container } = renderMany();
    const table = container.querySelector(".table-scroll > table.pdc-table");
    expect(table?.className).toContain("table--sticky-column");
    expect(table?.className).toContain("responsive-table");
    expect(container.querySelectorAll("thead th")).toHaveLength(9);
    expect(container.querySelector("thead th.table__sticky-cell")?.textContent).toBe("Ítem");
  });

  it("nombre largo: limitado a dos líneas con el texto completo en title, y checkbox con nombre accesible", () => {
    const { container } = renderMany([manyLines[0]]);
    const desc = container.querySelector("tbody .pdc-table__desc") as HTMLElement;
    expect(desc.textContent).toBe(longName);
    expect(desc.getAttribute("title")).toBe(longName);
    expect(screen.getByRole("checkbox", { name: `Incluir: ${longName}` })).toBeTruthy();
  });

  it("móvil: cada importe lleva su etiqueta (data-label) para el apilado compacto", () => {
    const { container } = renderMany([manyLines[1]]);
    const labels = Array.from(container.querySelectorAll("tbody td[data-label]")).map((td) => td.getAttribute("data-label"));
    expect(labels).toEqual([
      "Cantidad", "Costo unit. actual", "Subtotal base", "% particip.", "Valor asignado",
      "Asignado/unidad", "Nuevo costo unit.", "Nuevo total línea",
    ]);
    expect(container.querySelector("tbody td.pdc-table__item")?.hasAttribute("data-label")).toBe(false);
  });

  it("varios productos con selección parcial e importes con decimales: mismos importes y totales", () => {
    const { container } = renderMany();
    const boxes = container.querySelectorAll('tbody input[type="checkbox"]');
    fireEvent.click(boxes[4]);
    calculate(container, "37.85");

    const rows = Array.from(container.querySelectorAll("tbody tr"));
    expect(rows).toHaveLength(6);
    expect(rows[4].className).toContain("pdc-table__row--excluded");
    const allocated = rows.map((r) => r.querySelector('td[data-label="Valor asignado"]')?.textContent);
    expect(allocated).toEqual(["19.64", "2.25", "6.13", "4.05", "0.00", "5.79"]);

    expect(summaryValue(container, "Total distribuido")).toBe("37.85");
    expect(summaryValue(container, "Diferencia de redondeo / por cuadrar")).toBe("0.00");
    expect(summaryValue(container, "Subtotal base ítems incluidos")).toBe("345.79");
    expect(summaryValue(container, "Subtotal base todos los ítems")).toBe("437.02");
    expect(summaryValue(container, "Total factura después / CxP esperada")).toBe("752.71");
    expect(summaryValue(container, "Total factura antes")).toBe("714.86");
    expect(summaryValue(container, "Flete actual del documento")).toBe("4.50");
    expect(summaryValue(container, "Otros gastos actuales del documento")).toBe("0.00");
  });

  it("resumen: dos grupos con su resultado primero, sin perder ninguno de los ocho conceptos", () => {
    const { container } = renderMany();
    const groups = Array.from(container.querySelectorAll(".pdc-summary__group"));
    expect(groups.map((g) => g.querySelector(".pdc-summary__title")?.textContent)).toEqual(["Distribución", "Factura"]);
    expect(groups.map((g) => g.querySelectorAll(".pdc-totals__row")).map((r) => r.length)).toEqual([4, 4]);
    expect(groups.map((g) => g.querySelector(".pdc-totals__row--primary .zh-summary-grid__label")?.textContent))
      .toEqual(["Total distribuido", "Total factura después / CxP esperada"]);
  });
});


describe("DistributeCostModal ? base cero", () => {
  it("permite calcular y aplicar con descuento del 100 %", () => {
    const { container } = render(<I18nProvider><DistributeCostModal open
      lines={lines.map(l => ({ ...l, discountAmount: l.quantity * l.unitPrice, landedUnitCost: 0, totalLineCost: 0 }))}
      totalFreight={0} totalOtherCosts={0} grandTotal={0} onCancel={vi.fn()} onApply={vi.fn()} />
    </I18nProvider>);
    fireEvent.blur(container.querySelector(".pdc-toolbar input") as HTMLInputElement, { target: { value: "1" } });
    const calculate = screen.getByRole("button", { name: "Calcular" });
    expect(calculate.hasAttribute("disabled")).toBe(false);
    fireEvent.click(calculate);
    expect(screen.getByRole("button", { name: "Aplicar" }).hasAttribute("disabled")).toBe(false);
    expect(screen.queryByText("La base de los ?tems incluidos debe ser mayor a cero.")).toBeNull();
  });
});
