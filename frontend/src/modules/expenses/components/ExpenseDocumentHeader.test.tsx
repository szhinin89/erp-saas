// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { ExpenseDocumentHeader } from "./ExpenseDocumentHeader";
import type { ExpenseDocumentHeaderState } from "./ExpenseDocumentHeader";
import type { SriTaxSupportLookup } from "../../items/facades/sriLookupFacade";

/**
 * RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H — pruebas de componente aisladas para el campo "Código
 * sustento tributario" de la cabecera de Gastos. Complementa `expenseDocumentDraftModel.test.ts`
 * (normalización del payload) y `expenseDocumentService.retention.test.ts` (payload de
 * createConfirmedExpense) — aquí se prueba que el campo esté realmente en el formulario y que
 * dejarlo vacío sea un estado válido.
 */

vi.mock("../../masterData/api/businessPartnerFacade", () => ({
  businessPartnerFacade: { searchSuppliersForPicker: vi.fn() },
}));

afterEach(() => {
  cleanup();
});

const BASE_HEADER: ExpenseDocumentHeaderState = {
  supplierId: "sup-1",
  issueDate: "2026-09-01",
  accountingDate: "2026-09-01",
  documentType: "01",
  documentNumber: "001-001-000000001",
  paymentTermId: "",
  dueDate: "",
  authorizationNumber: "",
  authorizationDate: "",
  notes: "",
  taxSupportCode: "",
};

const SRI_TAX_SUPPORTS: SriTaxSupportLookup[] = [
  { code: "01", name: "Crédito Tributario para declaración de IVA" },
  { code: "02", name: "Costo o Gasto para declaración del IR" },
];

function renderHeader(overrides: Partial<ExpenseDocumentHeaderState> = {}) {
  const onChange = vi.fn();
  render(
    <ExpenseDocumentHeader
      value={{ ...BASE_HEADER, ...overrides }}
      supplier={null}
      paymentTerms={[]}
      sriDocTypes={[{ code: "01", name: "Factura", shortName: "FAC", isElectronic: true }]}
      sriTaxSupports={SRI_TAX_SUPPORTS}
      onChange={onChange}
      onSupplierChange={vi.fn()}
    />,
  );
  return { onChange };
}

describe("ExpenseDocumentHeader — Código sustento tributario", () => {
  it("el campo aparece en el formulario con su label y su texto de ayuda", () => {
    renderHeader();

    expect(screen.getByText("Código sustento tributario")).toBeTruthy();
    expect(
      screen.getByText(
        "Si se deja vacío, se usará el valor configurado para el proveedor cuando exista.",
      ),
    ).toBeTruthy();
  });

  it("el catálogo mostrado viene del servicio real (sriTaxSupports), nunca una lista hardcodeada", () => {
    renderHeader();

    expect(screen.getByText("01 — Crédito Tributario para declaración de IVA")).toBeTruthy();
    expect(screen.getByText("02 — Costo o Gasto para declaración del IR")).toBeTruthy();
  });

  it("el usuario puede dejarlo vacío — el <select> soporta la opción sin especificar por defecto", () => {
    renderHeader({ taxSupportCode: "" });

    const select = screen.getByLabelText(/^Código sustento tributario/) as HTMLSelectElement;
    expect(select.value).toBe("");
    expect(screen.getByText("— Sin especificar —")).toBeTruthy();
  });

  it("notifica el código elegido vía onChange cuando el usuario selecciona uno", () => {
    const { onChange } = renderHeader();

    fireEvent.change(screen.getByLabelText(/^Código sustento tributario/), {
      target: { value: "02" },
    });

    expect(onChange).toHaveBeenCalledWith({ taxSupportCode: "02" });
  });

  it("muestra el código ya guardado del documento cuando se edita un gasto existente", () => {
    renderHeader({ taxSupportCode: "02" });

    const select = screen.getByLabelText(/^Código sustento tributario/) as HTMLSelectElement;
    expect(select.value).toBe("02");
  });

  it("regresión: se comporta como solo lectura cuando disabled=true (documento confirmado)", () => {
    render(
      <ExpenseDocumentHeader
        value={{ ...BASE_HEADER, taxSupportCode: "02" }}
        supplier={null}
        paymentTerms={[]}
        sriDocTypes={[{ code: "01", name: "Factura", shortName: "FAC", isElectronic: true }]}
      sriTaxSupports={SRI_TAX_SUPPORTS}
        disabled
        onChange={vi.fn()}
        onSupplierChange={vi.fn()}
      />,
    );

    const select = screen.getByLabelText(/^Código sustento tributario/) as HTMLSelectElement;
    const docType = screen.getByLabelText(/^Tipo de documento/) as HTMLSelectElement;
    expect(docType.disabled).toBe(true);
    expect(docType.selectedOptions[0].textContent).toBe("01 - Factura");
    expect(select.disabled).toBe(true);
    expect(select.value).toBe("02");
  });
});


describe("ExpenseDocumentHeader - tipos de documento SRI", () => {
  it("muestra código y nombre y conserva el código seleccionado", () => {
    renderHeader();
    const select = screen.getByLabelText(/^Tipo de documento/) as HTMLSelectElement;
    expect(select.value).toBe("01");
    expect(select.selectedOptions[0].textContent).toBe("01 - Factura");
  });

  it("conserva un código histórico ausente del catálogo", () => {
    const { onChange } = renderHeader({ documentType: "99" });
    const select = screen.getByLabelText(/^Tipo de documento/) as HTMLSelectElement;
    expect(select.value).toBe("99");
    expect(select.selectedOptions[0].textContent).toBe("99 - Tipo de documento no encontrado en catálogo");
    expect(onChange).not.toHaveBeenCalled();
    fireEvent.change(select, { target: { value: "01" } });
    expect(onChange).toHaveBeenCalledWith({ documentType: "01" });
  });
});
