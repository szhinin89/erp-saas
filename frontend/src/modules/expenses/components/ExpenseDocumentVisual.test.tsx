// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ExpenseDocumentHeader } from "./ExpenseDocumentHeader";
import type { ExpenseDocumentHeaderState } from "./ExpenseDocumentHeader";
import { ExpenseDocumentLinesEditor } from "./ExpenseDocumentLinesEditor";
import type { ExpenseDraftLineState } from "./ExpenseDocumentLinesEditor";
import { ExpenseRetentionSection } from "./ExpenseRetentionSection";
import { ExpenseDocumentTotals } from "./ExpenseDocumentTotals";
import { emptyRetentionIntentState } from "../utils/expenseRetentionModel";

/**
 * EXPENSE-UI-02 — contrato visual del formulario de Gastos: checkbox de retención en una sola
 * línea (label oficial `.zh-checkbox-label`), origen XML vs. clasificación manual, grupos de
 * cabecera y autorización larga completa/sin truncar.
 */

vi.mock("../../masterData/api/businessPartnerFacade", () => ({
  businessPartnerFacade: { searchBusinessPartners: vi.fn(), getBusinessPartner: vi.fn() },
}));
vi.mock("../../masterData/facades/businessPartnerPurchaseSettingsFacade", () => ({
  businessPartnerPurchaseSettingsFacade: { getPurchaseSettings: vi.fn() },
}));
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ has: () => false }),
}));
vi.mock("../api/expenseDocumentService", () => ({
  expenseDocumentService: {
    getRetentionEligibility: vi.fn().mockResolvedValue({
      isEligible: true,
      canRetainVat: true,
      canRetainIncome: true,
      isSupplierExempt: false,
      missingRetentionCode: false,
      reasons: [],
      candidates: [],
    }),
    getExpenseRetention: vi.fn(),
  },
}));

afterEach(cleanup);

const AUTH = "1234567890123456789012345678901234567890123456789";

const HEADER: ExpenseDocumentHeaderState = {
  supplierId: "sup-1",
  issueDate: "2026-09-01",
  accountingDate: "2026-09-01",
  documentType: "01",
  documentNumber: "001-001-000000001",
  paymentTermId: "",
  dueDate: "",
  authorizationNumber: AUTH,
  authorizationDate: "",
  notes: "",
  taxSupportCode: "",
};

function renderHeader(xmlFields?: ReadonlySet<keyof ExpenseDocumentHeaderState>) {
  return render(
    <ExpenseDocumentHeader
      value={HEADER}
      supplier={null}
      paymentTerms={[]}
      sriDocTypes={[{ code: "01", name: "Factura", shortName: "FAC", isElectronic: true }]}
      sriTaxSupports={[]}
      xmlFields={xmlFields}
      onChange={vi.fn()}
      onSupplierChange={vi.fn()}
    />,
    { wrapper: I18nProvider },
  );
}

const line = (over: Partial<ExpenseDraftLineState> = {}): ExpenseDraftLineState => ({
  key: "l1",
  expenseSubcategoryId: "",
  description: "",
  quantity: "1",
  unitPrice: "10.00",
  discountValue: "0.00",
  vatCode: "0",
  notes: "",
  ...over,
});

function renderLines(lines: ExpenseDraftLineState[], fromXml: boolean) {
  return render(
    <ExpenseDocumentLinesEditor
      lines={lines}
      tree={[]}
      accountsById={new Map()}
      vatRates={[{ code: "0", name: "0% IVA", percentage: 0 }]}
      vatRateByCode={new Map([["0", 0]])}
      fromXml={fromXml}
      onChange={vi.fn()}
    />,
    { wrapper: I18nProvider },
  );
}

describe("ExpenseDocumentHeader — presentación (EXPENSE-UI-02)", () => {
  it("agrupa los campos y conserva todos", () => {
    renderHeader();
    expect(screen.getByRole("group", { name: "Proveedor y comprobante" })).toBeTruthy();
    expect(screen.getByRole("group", { name: "Fechas y pago" })).toBeTruthy();
    expect(screen.getByLabelText(/^Autorización/)).toBeTruthy();
    expect(screen.getByLabelText(/^Fecha contable/)).toBeTruthy();
  });

  it("muestra el número de autorización completo, sin truncar ni recalcular", () => {
    renderHeader();
    const input = screen.getByLabelText(/^Autorización/) as HTMLInputElement;
    expect(input.value).toBe(AUTH);
    expect(input.className).toContain("exp-doc-code-input");
  });

  it("marca con XML solo los campos que vienen del comprobante", () => {
    renderHeader(new Set(["documentNumber", "authorizationNumber"]));
    expect(screen.getAllByText("Del XML").length).toBe(2);
    expect(screen.getByLabelText(/^Autorización/).closest("label")?.textContent).toContain("Del XML");
    expect(screen.getByLabelText(/^Fecha contable/).closest("label")?.textContent).not.toContain("Del XML");
  });

  it("sin XML no muestra insignias ni leyenda", () => {
    renderHeader();
    expect(screen.queryByText("Del XML")).toBeNull();
  });
});

describe("ExpenseDocumentLinesEditor — presentación (EXPENSE-UI-02)", () => {
  it("con XML marca categoría por asignar en cada concepto sin subcategoría", () => {
    renderLines([line(), line({ key: "l2" })], true);
    expect(screen.getAllByText("Categoría por asignar").length).toBe(2);
  });

  it("sin XML no marca clasificación manual; con varios conceptos conserva totales y borrado por línea", () => {
    renderLines([line(), line({ key: "l2" })], false);
    expect(screen.queryByText("Categoría por asignar")).toBeNull();
    expect(screen.getAllByText("Total").length).toBe(2);
    expect(screen.getAllByRole("button", { name: /Quitar concepto/ }).length).toBe(2);
  });
});

describe("ExpenseRetentionSection — checkbox (EXPENSE-UI-02)", () => {
  it("control y texto comparten un único label oficial .zh-checkbox-label", async () => {
    render(
      <ExpenseRetentionSection
        expenseDocumentId="doc-1"
        documentStatus="Draft"
        refreshKey={0}
        value={emptyRetentionIntentState()}
        onChange={vi.fn()}
        onEligibilityChange={vi.fn()}
      />,
      { wrapper: I18nProvider },
    );
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Aplicar retención a este gasto") as HTMLInputElement).disabled,
      ).toBe(false),
    );
    const input = screen.getByLabelText("Aplicar retención a este gasto");
    const label = input.closest("label");
    expect(label?.classList.contains("zh-checkbox-label")).toBe(true);
    expect(label?.textContent).toContain("Generar retención al confirmar este gasto");
    // Sin ZHField alrededor: ya no hereda `.zh-field-control input { width: 100% }`.
    expect(input.closest(".zh-field-control")).toBeNull();
  });
});

describe("ExpenseDocumentHeader — lenguaje y ayudas (EXPENSE-UI-02)", () => {
  it("usa términos comerciales y no deja instrucciones permanentes", () => {
    renderHeader();
    expect(screen.getByRole("heading", { name: "Datos del comprobante" })).toBeTruthy();
    expect(screen.queryByText("Cabecera")).toBeNull();
    expect(screen.getByRole("button", { name: "Datos del comprobante" })).toBeTruthy();
  });
});

describe("ExpenseDocumentLinesEditor — compactación (EXPENSE-UI-02)", () => {
  it("usa «Conceptos del gasto» y deja la observación plegada hasta que se pide o ya tiene texto", () => {
    renderLines([line(), line({ key: "l2", notes: "Nota existente" })], false);
    expect(screen.getByRole("heading", { name: "Conceptos del gasto" })).toBeTruthy();
    // vacío → solo el botón; con texto → el campo abierto, sin ocultar el dato.
    expect(screen.getAllByRole("button", { name: "Agregar observación" }).length).toBe(1);
    expect((screen.getByLabelText("Observación") as HTMLTextAreaElement).value).toBe("Nota existente");
    fireEvent.click(screen.getByRole("button", { name: "Agregar observación" }));
    expect(screen.getAllByLabelText("Observación").length).toBe(2);
  });
});

describe("ExpenseDocumentTotals — siempre disponible (EXPENSE-UI-02)", () => {
  const totals = { subtotal: 100, totalDiscount: 10, totalTax: 13.5, grandTotal: 103.5 };

  it("muestra Subtotal, Descuento, IVA y Total, y refleja de inmediato un cambio de importes", () => {
    const { rerender } = render(<ExpenseDocumentTotals totals={totals} />, { wrapper: I18nProvider });
    const summary = screen.getByRole("region", { name: "Resumen de importes" });
    for (const label of ["Subtotal", "Descuento", "IVA", "Total"]) {
      expect(summary.textContent).toContain(label);
    }
    expect(summary.textContent).toContain("103.50");
    rerender(<ExpenseDocumentTotals totals={{ ...totals, grandTotal: 200, totalTax: 20 }} />);
    expect(summary.textContent).toContain("200.00");
    expect(summary.textContent).not.toContain("103.50");
  });

  it("el desglose móvil se alterna con un botón de disclosure accesible", () => {
    render(<ExpenseDocumentTotals totals={totals} />, { wrapper: I18nProvider });
    const toggle = screen.getByRole("button", { name: "Ver desglose" });
    expect(toggle.getAttribute("aria-expanded")).toBe("false");
    const rows = document.getElementById(toggle.getAttribute("aria-controls") ?? "");
    expect(rows?.classList.contains("exp-doc-totals__rows--open")).toBe(false);
    fireEvent.click(toggle);
    expect(screen.getByRole("button", { name: "Ocultar desglose" }).getAttribute("aria-expanded")).toBe("true");
    expect(rows?.classList.contains("exp-doc-totals__rows--open")).toBe(true);
  });
});
