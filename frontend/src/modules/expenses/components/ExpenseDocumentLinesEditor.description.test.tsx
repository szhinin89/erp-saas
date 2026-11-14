// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ExpenseDocumentLinesEditor } from "./ExpenseDocumentLinesEditor";
import type { ExpenseDraftLineState } from "./ExpenseDocumentLinesEditor";

/**
 * EXPENSE-UX-01 — B1: "Descripción" es opcional (el backend usa el nombre de la subcategoría si
 * llega vacía; ver ExpenseDraftRules.BuildLinesAsync), por lo que la UI no la marca como
 * obligatoria. B2 (parte visual): los errores por línea/campo se muestran en su campo.
 */

afterEach(cleanup);

function makeLine(overrides: Partial<ExpenseDraftLineState> = {}): ExpenseDraftLineState {
  return {
    key: "line-1",
    expenseSubcategoryId: "",
    description: "",
    quantity: "1",
    unitPrice: "90.00",
    discountValue: "0.00",
    vatCode: "0",
    notes: "",
    ...overrides,
  };
}

// Marcador visual "*" que ZHField agrega a la etiqueta de un campo obligatorio.
const requiredMarker = (control: HTMLElement) =>
  control.closest("label")?.querySelector(".zh-field-required") ?? null;

function renderEditor(
  lines: ExpenseDraftLineState[],
  errors?: Record<string, Partial<Record<keyof ExpenseDraftLineState, string>>>,
) {
  return render(
    <ExpenseDocumentLinesEditor
      lines={lines}
      tree={[]}
      accountsById={new Map()}
      vatRates={[{ code: "0", name: "0% IVA", percentage: 0 }]}
      vatRateByCode={new Map([["0", 0]])}
      errors={errors}
      onChange={vi.fn()}
    />,
    { wrapper: I18nProvider },
  );
}

describe("ExpenseDocumentLinesEditor — Descripción opcional (B1)", () => {
  it("no marca Descripción como obligatoria y explica el comportamiento real", () => {
    renderEditor([makeLine()]);

    const description = screen.getByLabelText(/^Descripción/) as HTMLInputElement;
    expect(description.required).toBe(false);
    expect(requiredMarker(description)).toBeNull();
    expect(
      screen.getByText("Opcional: si se deja vacía, se usa el nombre de la subcategoría."),
    ).toBeTruthy();
  });

  it("los campos realmente obligatorios siguen marcados como tales", () => {
    renderEditor([makeLine()]);

    for (const label of [/^Cantidad/, /^Valor unitario/, /^Código IVA/]) {
      expect(requiredMarker(screen.getByLabelText(label))).not.toBeNull();
    }
  });
});

describe("ExpenseDocumentLinesEditor — errores por línea y campo (B2)", () => {
  it("muestra cada error en la línea y el campo que corresponde, sin contaminar otras líneas", () => {
    renderEditor(
      [makeLine({ key: "k1" }), makeLine({ key: "k2" })],
      {
        k2: {
          quantity: "La cantidad debe ser mayor a cero.",
          vatCode: "El codigo IVA es obligatorio por linea.",
        },
      },
    );

    expect(screen.getAllByText("La cantidad debe ser mayor a cero.")).toHaveLength(1);
    expect(screen.getAllByText("El codigo IVA es obligatorio por linea.")).toHaveLength(1);

    const quantityOfSecondLine = screen.getAllByLabelText(/^Cantidad/)[1] as HTMLElement;
    expect(quantityOfSecondLine.getAttribute("aria-invalid")).toBe("true");
    const quantityOfFirstLine = screen.getAllByLabelText(/^Cantidad/)[0] as HTMLElement;
    expect(quantityOfFirstLine.getAttribute("aria-invalid")).not.toBe("true");
  });

  it("un error de Descripción reemplaza a la ayuda opcional solo en esa línea", () => {
    renderEditor([makeLine({ key: "k1" })], {
      k1: { description: "La descripción excede 300 caracteres." },
    });

    expect(screen.getByText("La descripción excede 300 caracteres.")).toBeTruthy();
    expect(
      screen.queryByText("Opcional: si se deja vacía, se usa el nombre de la subcategoría."),
    ).toBeNull();
  });
});
