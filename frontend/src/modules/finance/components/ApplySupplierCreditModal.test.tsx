// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ApplySupplierCreditModal } from "./ApplySupplierCreditModal";
import { I18nProvider } from "../../../i18n/i18n";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import type { SupplierCreditDto } from "../api/supplierCreditService";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04B — "Monto a aplicar" declara `precision="money"` (antes
 * `decimals={2}`). La escala sale de la PrecisionPolicy; payload y validación sin cambios.
 */

const apply = vi.fn();
vi.mock("../api/supplierCreditService", () => ({
  supplierCreditService: { apply: (...a: unknown[]) => apply(...a) },
}));
// 02D-C — CxP elegibles por estado (pending / partiallypaid), de Compra y de Gasto.
const PAYABLES_BY_STATUS: Record<string, unknown[]> = {
  pending: [
    { id: "pay-1", originType: "PurchaseInvoice", documentNumber: "FAC-001", outstandingAmount: 80 },
    { id: "pay-man", originType: "Manual", documentNumber: "MAN-001", outstandingAmount: 10 },
  ],
  partiallypaid: [
    { id: "pay-2", originType: "ExpenseDocument", documentNumber: "GAS-007", outstandingAmount: 25 },
    { id: "pay-0", originType: "ExpenseDocument", documentNumber: "GAS-000", outstandingAmount: 0 },
  ],
};
const list = vi.fn((filters: { status: string }) =>
  Promise.resolve({ items: PAYABLES_BY_STATUS[filters.status] ?? [] }),
);
vi.mock("../../payables/facades/payableLookupFacade", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../payables/facades/payableLookupFacade")>()),
  payableLookupFacade: { list: (...a: unknown[]) => list(...(a as [{ status: string }])) },
}));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

afterEach(() => {
  cleanup();
  apply.mockReset();
  list.mockClear();
});

const CREDIT = {
  id: "cred-1",
  supplierId: "sup-1",
  supplierName: "Distribuidora Andina",
  availableAmount: 50,
} as SupplierCreditDto;

async function renderModal(defaultPayableId?: string) {
  render(
    <I18nProvider>
      <ApplySupplierCreditModal
        open
        credit={CREDIT}
        onClose={() => {}}
        onApplied={() => {}}
        defaultPayableId={defaultPayableId}
      />
    </I18nProvider>,
  );
  await waitFor(() => expect(screen.getByText(/FAC-001/)).toBeTruthy());
  const amount = document.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
  return { amount };
}

describe("ApplySupplierCreditModal — precision='money' (04B)", () => {
  it("edición con coma → valor canónico; el payload de apply conserva el contrato (amount numérico)", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    apply.mockResolvedValue({ ...CREDIT, availableAmount: 37.5 });
    const { amount } = await renderModal();
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "pay-1" } });
    fireEvent.focus(amount);
    fireEvent.paste(amount, { clipboardData: { getData: () => "12,5" } });
    fireEvent.blur(amount);
    expect(amount.value).toBe("12.50");
    await act(async () => {
      fireEvent.click(screen.getByText("Aplicar crédito"));
    });
    await waitFor(() => expect(apply).toHaveBeenCalledTimes(1));
    expect(apply.mock.calls[0]![0]).toBe("cred-1");
    expect(apply.mock.calls[0]![1]).toMatchObject({ targetPurchasePayableId: "pay-1", amount: 12.5 });
  });

  it("policy sintética moneyDecimals=3: el teclado admite 3 decimales y bloquea el 4.º", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 });
    const { amount } = await renderModal();
    fireEvent.change(amount, { target: { value: "1.23" } });
    amount.setSelectionRange(4, 4);
    expect(fireEvent.keyDown(amount, { key: "4" })).toBe(true);
    fireEvent.change(amount, { target: { value: "1.234" } });
    amount.setSelectionRange(5, 5);
    expect(fireEvent.keyDown(amount, { key: "5" })).toBe(false);
  });
});

describe("ApplySupplierCreditModal — CxP de Compra y de Gasto (02D-C)", () => {
  it("lista CxP pendientes y parcialmente pagadas de Compra y Gasto con origen, documento y saldo pendiente", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    await renderModal();

    expect(list).toHaveBeenCalledTimes(2);
    for (const [filters] of list.mock.calls) {
      expect(filters).toMatchObject({ supplierId: "sup-1" });
      expect(filters).not.toHaveProperty("originType");
    }
    expect(list.mock.calls.map(([f]) => f.status).sort()).toEqual(["partiallypaid", "pending"]);

    const options = Array.from(document.querySelectorAll("option")).map((o) => o.textContent ?? "");
    expect(options.some((t) => t.includes("Compra") && t.includes("FAC-001") && t.includes("Saldo pendiente") && t.includes("80.00"))).toBe(true);
    expect(options.some((t) => t.includes("Gasto") && t.includes("GAS-007") && t.includes("25.00"))).toBe(true);
    expect(options.some((t) => t.includes("MAN-001"))).toBe(false);
    expect(options.some((t) => t.includes("GAS-000"))).toBe(false);
  });

  it("aplica contra una CxP de Gasto enviando su Id como destino", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    apply.mockResolvedValue({ ...CREDIT, availableAmount: 40 });
    const { amount } = await renderModal();
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "pay-2" } });
    fireEvent.change(amount, { target: { value: "10" } });
    fireEvent.blur(amount);
    await act(async () => {
      fireEvent.click(screen.getByText("Aplicar crédito"));
    });
    await waitFor(() => expect(apply).toHaveBeenCalledTimes(1));
    expect(apply.mock.calls[0]![1]).toMatchObject({ targetPurchasePayableId: "pay-2", amount: 10 });
  });
});

describe("ApplySupplierCreditModal — CxP preseleccionada desde la CxP (02D-F)", () => {
  it("preselecciona la CxP destino elegible y la envía al aplicar", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    apply.mockResolvedValue({ ...CREDIT, availableAmount: 40 });
    const { amount } = await renderModal("pay-2");
    await waitFor(() =>
      expect((screen.getByRole("combobox") as HTMLSelectElement).value).toBe("pay-2"),
    );
    fireEvent.change(amount, { target: { value: "10" } });
    fireEvent.blur(amount);
    await act(async () => {
      fireEvent.click(screen.getByText("Aplicar crédito"));
    });
    await waitFor(() => expect(apply).toHaveBeenCalledTimes(1));
    expect(apply.mock.calls[0]![1]).toMatchObject({ targetPurchasePayableId: "pay-2" });
  });

  it("ignora una CxP preseleccionada que no es elegible", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    await renderModal("pay-man");
    expect((screen.getByRole("combobox") as HTMLSelectElement).value).toBe("");
  });
});

describe("ApplySupplierCreditModal — sin GUID visible (02D QA)", () => {
  it("el subtítulo muestra el nombre del proveedor, nunca su Id", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    await renderModal();
    expect(screen.getByText(/Proveedor: Distribuidora Andina/)).toBeTruthy();
    expect(screen.queryByText(/sup-1/)).toBeNull();
  });
});
