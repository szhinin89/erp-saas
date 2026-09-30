// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import type { SalesReceivableDto } from "../api/receivableService";

vi.mock("../api/paymentService", () => ({ paymentService: { registerCollection: vi.fn() } }));
vi.mock("../api/bankAccountService", () => ({ bankAccountService: { list: vi.fn().mockResolvedValue([]) } }));
vi.mock("../../sales/facades/paymentMethodLookupFacade", () => ({
  paymentMethodLookupFacade: { list: vi.fn().mockResolvedValue([]) },
}));
vi.mock("../../caja/facades/cashRegisterLookupFacade", () => ({
  cashRegisterLookupFacade: { getCashRegisters: vi.fn().mockResolvedValue([]) },
}));
vi.mock("../../../lib/messages", () => ({ message: { success: vi.fn(), error: vi.fn() } }));

import { paymentService } from "../api/paymentService";
import { RegisterCollectionModal } from "./RegisterCollectionModal";

/**
 * ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — el cobro viaja con un ClientRequestId estable por
 * intención: doble clic = una sola llamada; reintento tras fallo = el MISMO id (el backend
 * responde el cobro original si el primer intento sí se registró).
 */
const RECEIVABLE: SalesReceivableDto = {
  id: "ar-1",
  invoiceId: "inv-1",
  invoiceNumber: "001-001-000000001",
  customerId: "cust-1",
  customerName: "Cliente Uno",
  customerIdentification: "1710034065",
  branchId: "br-1",
  branchName: "Matriz",
  createdByUserId: null,
  createdByName: null,
  invoiceIssuedAt: "2026-09-01",
  invoiceCreatedAt: "2026-09-01T10:00:00Z",
  dueDate: "2026-10-01",
  originalAmount: 100,
  paidAmount: 0,
  balanceDue: 100,
  status: "pending",
  statusLabel: "Pendiente",
  installmentCount: 1,
  overdueDays: null,
  installments: [],
  createdAt: "2026-09-01T10:00:00Z",
  updatedAt: null,
};

function renderModal(onRegistered = vi.fn()) {
  render(
    <I18nProvider>
      <RegisterCollectionModal open receivable={RECEIVABLE} onClose={vi.fn()} onRegistered={onRegistered} />
    </I18nProvider>,
  );
}

beforeEach(() => setPrecisionPolicyForTests(TEST_PRECISION_POLICY));
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("RegisterCollectionModal — idempotencia de la intención", () => {
  it("doble clic en Registrar cobro: una sola llamada al backend", async () => {
    let finish!: () => void;
    vi.mocked(paymentService.registerCollection).mockImplementation(
      () => new Promise((resolve) => (finish = () => resolve({ id: "pay-1" } as never))),
    );
    renderModal();
    const save = await screen.findByRole("button", { name: "Registrar cobro" });

    fireEvent.click(save);
    fireEvent.click(save);
    await waitFor(() => expect(paymentService.registerCollection).toHaveBeenCalled());
    finish();

    await waitFor(() => expect(screen.getByRole("button", { name: "Registrar cobro" })).toBeTruthy());
    expect(paymentService.registerCollection).toHaveBeenCalledTimes(1);
  });

  it("reintentar tras un fallo reenvía el MISMO clientRequestId", async () => {
    vi.mocked(paymentService.registerCollection)
      .mockRejectedValueOnce(new Error("timeout"))
      .mockResolvedValueOnce({ id: "pay-1" } as never);
    const onRegistered = vi.fn();
    renderModal(onRegistered);

    fireEvent.click(await screen.findByRole("button", { name: "Registrar cobro" }));
    await waitFor(() => expect(paymentService.registerCollection).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByRole("button", { name: "Registrar cobro" })).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "Registrar cobro" }));
    await waitFor(() => expect(onRegistered).toHaveBeenCalled());

    const [first, retry] = vi.mocked(paymentService.registerCollection).mock.calls.map(([p]) => p.clientRequestId);
    expect(first).toMatch(/^[0-9a-f-]{36}$/);
    expect(retry).toBe(first);
  });
});
