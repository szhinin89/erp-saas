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
vi.mock("../../payables/api/payablesService", () => ({
  payablesService: {
    list: () =>
      Promise.resolve({ items: [{ id: "pay-1", documentNumber: "FAC-001", outstandingAmount: 80 }] }),
  },
}));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

afterEach(() => {
  cleanup();
  apply.mockReset();
});

const CREDIT = { id: "cred-1", supplierId: "sup-1", availableAmount: 50 } as SupplierCreditDto;

async function renderModal() {
  render(
    <I18nProvider>
      <ApplySupplierCreditModal open credit={CREDIT} onClose={() => {}} onApplied={() => {}} />
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
