// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useState } from "react";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ExpenseDocumentHeader } from "./ExpenseDocumentHeader";
import type { ExpenseDocumentHeaderState } from "./ExpenseDocumentHeader";
import type { PaymentTermDto } from "../../masterData/facades/paymentTermLookupFacade";
import type { SupplierPickerRow } from "../../masterData/facades/supplierPickerFacade";

/**
 * EXPENSE-UX-01 (U6) — la condición de pago efectiva se muestra con su origen (proveedor, selección
 * manual o ya registrada en el documento). Solo presentación: la resolución real sigue en el backend.
 */

const purchaseSettingsFacade = vi.hoisted(() => ({ getPurchaseSettings: vi.fn() }));
vi.mock("../../masterData/facades/businessPartnerPurchaseSettingsFacade", () => ({
  businessPartnerPurchaseSettingsFacade: purchaseSettingsFacade,
}));

const SUPPLIERS: Record<string, SupplierPickerRow> = {
  a: row("sup-a", "Proveedor A"),
  b: row("sup-b", "Proveedor B"),
  c: row("sup-c", "Proveedor C"),
};

function row(id: string, fullName: string): SupplierPickerRow {
  return {
    id,
    fullName,
    identificationNumber: "1790000000001",
    isActive: true,
    hasSupplierRole: true,
    supplierConfig: null,
  };
}

// Sustituye el buscador (cubierto en su propio test) por botones que emiten la elección.
vi.mock("../../masterData/facades/supplierPickerFacade", () => ({
  SupplierSearchSelect: ({
    onChange,
  }: {
    onChange: (supplier: SupplierPickerRow | null) => void;
  }) => (
    <div>
      {Object.entries(SUPPLIERS).map(([key, supplier]) => (
        <button key={key} type="button" onClick={() => onChange(supplier)}>
          {`Elegir ${supplier.fullName}`}
        </button>
      ))}
      <button type="button" onClick={() => onChange(null)}>
        Quitar proveedor
      </button>
    </div>
  ),
}));

const PAYMENT_TERMS = [
  { id: "pt-30", code: "30D", summary: "30 días", isActive: true },
  { id: "pt-cash", code: "CONT", summary: "Contado", isActive: true },
] as unknown as PaymentTermDto[];

const BASE_HEADER: ExpenseDocumentHeaderState = {
  supplierId: "",
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

function Harness({ initial }: Readonly<{ initial?: Partial<ExpenseDocumentHeaderState> }>) {
  const [header, setHeader] = useState<ExpenseDocumentHeaderState>({ ...BASE_HEADER, ...initial });
  const [supplier, setSupplier] = useState<SupplierPickerRow | null>(null);
  return (
    <ExpenseDocumentHeader
      value={header}
      supplier={supplier}
      paymentTerms={PAYMENT_TERMS}
      sriDocTypes={[{ code: "01", name: "Factura", shortName: "FAC", isElectronic: true }]}
      sriTaxSupports={[]}
      onChange={(patch) => setHeader((current) => ({ ...current, ...patch }))}
      onSupplierChange={setSupplier}
    />
  );
}

const termSelect = () => screen.getByLabelText(/^Condición de pago/) as HTMLSelectElement;
// El buscador simulado vive dentro de un <label> (ZHField): el primer botón toma el nombre del
// label, así que se localiza por su texto visible.
const pick = (name: string) => fireEvent.click(screen.getByText(name));

const HINT_AWAIT = "Seleccione un proveedor para ver su condición de pago predeterminada.";
const HINT_FROM_SUPPLIER = "Tomada de la condición predeterminada del proveedor.";
const HINT_EXPLICIT = "Seleccionada manualmente.";
const HINT_SAVED = "Registrada en el documento.";
const HINT_WITHOUT = "El proveedor no tiene condición de pago predeterminada: seleccione una.";

beforeEach(() => {
  purchaseSettingsFacade.getPurchaseSettings.mockReset();
  purchaseSettingsFacade.getPurchaseSettings.mockImplementation(async (id: string) => {
    if (id === "sup-a") return { paymentTermId: "pt-30" };
    if (id === "sup-b") return { paymentTermId: null };
    throw new Error("network");
  });
});

afterEach(cleanup);

function renderHarness(initial?: Partial<ExpenseDocumentHeaderState>) {
  return render(<Harness initial={initial} />, { wrapper: I18nProvider });
}

describe("ExpenseDocumentHeader — origen de la condición de pago (U6)", () => {
  it("sin proveedor invita a seleccionarlo para conocer su condición", () => {
    renderHarness();
    expect(screen.getByText(HINT_AWAIT)).toBeTruthy();
    expect(termSelect().value).toBe("");
  });

  it("al elegir un proveedor con default, lo pre-llena e indica que viene del proveedor", async () => {
    renderHarness();
    pick("Elegir Proveedor A");
    expect(await screen.findByText(HINT_FROM_SUPPLIER)).toBeTruthy();
    expect(termSelect().value).toBe("pt-30");
  });

  it("una elección manual se indica como tal y se preserva al cambiar de proveedor", async () => {
    renderHarness();
    pick("Elegir Proveedor A");
    await screen.findByText(HINT_FROM_SUPPLIER);

    fireEvent.change(termSelect(), { target: { value: "pt-cash" } });
    expect(screen.getByText(HINT_EXPLICIT)).toBeTruthy();

    pick("Elegir Proveedor B");
    expect(termSelect().value).toBe("pt-cash");
    expect(screen.getByText(HINT_EXPLICIT)).toBeTruthy();
  });

  it("un valor pre-llenado por el proveedor anterior se reevalúa con el proveedor nuevo", async () => {
    renderHarness();
    pick("Elegir Proveedor A");
    await screen.findByText(HINT_FROM_SUPPLIER);

    pick("Elegir Proveedor B");
    expect(await screen.findByText(HINT_WITHOUT)).toBeTruthy();
    expect(termSelect().value).toBe("");
  });

  it("proveedor sin default y sin selección: avisa que debe elegir una condición", async () => {
    renderHarness();
    pick("Elegir Proveedor B");
    expect(await screen.findByText(HINT_WITHOUT)).toBeTruthy();
    expect(termSelect().value).toBe("");
  });

  it("si el usuario vacía la condición, indica cuál usará el servidor (la del proveedor)", async () => {
    renderHarness();
    pick("Elegir Proveedor A");
    await screen.findByText(HINT_FROM_SUPPLIER);

    fireEvent.change(termSelect(), { target: { value: "" } });
    expect(
      await screen.findByText(
        "Al guardar se usará la condición predeterminada del proveedor: 30D - 30 días.",
      ),
    ).toBeTruthy();
  });

  it("si la consulta del proveedor falla, no se oculta: avisa y no inventa una condición", async () => {
    renderHarness();
    pick("Elegir Proveedor C");
    expect(
      await screen.findByText(
        "No se pudo consultar la condición predeterminada del proveedor; al guardar el servidor aplicará la del proveedor si existe.",
      ),
    ).toBeTruthy();
    expect(termSelect().value).toBe("");
  });

  it("una condición ya registrada en el documento se indica como registrada", () => {
    renderHarness({ supplierId: "sup-a", paymentTermId: "pt-30" });
    expect(screen.getByText(HINT_SAVED)).toBeTruthy();
    expect(termSelect().value).toBe("pt-30");
  });

  it("una elección manual durante la consulta en vuelo no es pisada por el default", async () => {
    let resolveSettings: (value: { paymentTermId: string | null }) => void = () => {};
    purchaseSettingsFacade.getPurchaseSettings.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveSettings = resolve;
        }),
    );
    renderHarness();
    pick("Elegir Proveedor A");
    fireEvent.change(termSelect(), { target: { value: "pt-cash" } });
    resolveSettings({ paymentTermId: "pt-30" });

    await waitFor(() => expect(purchaseSettingsFacade.getPurchaseSettings).toHaveBeenCalledTimes(1));
    expect(termSelect().value).toBe("pt-cash");
    expect(screen.getByText(HINT_EXPLICIT)).toBeTruthy();
  });
});
