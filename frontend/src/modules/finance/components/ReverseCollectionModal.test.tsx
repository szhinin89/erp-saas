// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import type { SalesReceivableDto } from "../api/receivableService";
import { paymentService, type PaymentDto } from "../api/paymentService";
import { ReverseCollectionModal } from "./ReverseCollectionModal";

vi.mock("../api/paymentService", () => ({ paymentService: {
  collectionsByReceivable: vi.fn(), reverseCollection: vi.fn(),
} }));
vi.mock("../../../lib/messages", () => ({ message: { success: vi.fn() } }));

const receivable = { id: "ar-1", invoiceNumber: "001-001-000000123", customerName: "Cliente" } as SalesReceivableDto;
const payment = { id: "payment-1", amount: 40, paymentDate: "2026-10-28", status: "Applied",
  reference: "Referencia única", reverseReason: null, reversedAtUtc: null } as PaymentDto;

function renderModal() {
  const onClose = vi.fn();
  const onReversed = vi.fn();
  render(<I18nProvider><ReverseCollectionModal receivable={receivable} onClose={onClose} onReversed={onReversed} /></I18nProvider>);
  return { onClose, onReversed };
}

beforeEach(() => {
  setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
  vi.mocked(paymentService.collectionsByReceivable).mockResolvedValue([payment]);
  vi.mocked(paymentService.reverseCollection).mockResolvedValue({ ...payment, status: "Reversed" });
});
afterEach(() => { cleanup(); vi.resetAllMocks(); });

describe("Anulación documental de cobros", () => {
  it("requiere selección, confirmación y motivo no vacío", async () => {
    renderModal();
    expect(screen.queryByText("Confirmar anulación")).toBeNull();
    fireEvent.click(await screen.findByRole("button", { name: "Anular cobro" }));
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "   " } });
    fireEvent.click(screen.getByText("Confirmar anulación"));
    expect(await screen.findByText("El motivo es obligatorio.")).toBeTruthy();
    expect(paymentService.reverseCollection).not.toHaveBeenCalled();
  });

  it("envía el cobro seleccionado y motivo, actualiza CxC y habilita nuevo cobro", async () => {
    const callbacks = renderModal();
    fireEvent.click(await screen.findByRole("button", { name: "Anular cobro" }));
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: " Error de registro " } });
    fireEvent.click(screen.getByText("Confirmar anulación"));
    await waitFor(() => expect(paymentService.reverseCollection).toHaveBeenCalledWith("payment-1", "Error de registro"));
    await waitFor(() => expect(callbacks.onReversed).toHaveBeenCalledOnce());
    expect(callbacks.onClose).toHaveBeenCalledOnce();
  });

  it("conserva visibles los cobros anulados sin ofrecer segunda anulación", async () => {
    vi.mocked(paymentService.collectionsByReceivable).mockResolvedValue([{ ...payment,
      status: "Reversed", reverseReason: "Registro duplicado", reversedAtUtc: "2026-10-28T15:00:00Z" }]);
    renderModal();
    expect(await screen.findByText("Registro duplicado")).toBeTruthy();
    expect(screen.getByText("Anulado")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Anular cobro" })).toBeNull();
  });

  it("un rechazo conserva el formulario y no anuncia éxito", async () => {
    vi.mocked(paymentService.reverseCollection).mockRejectedValue(new Error("Bloqueado"));
    const callbacks = renderModal();
    fireEvent.click(await screen.findByRole("button", { name: "Anular cobro" }));
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Error" } });
    fireEvent.click(screen.getByText("Confirmar anulación"));
    expect(await screen.findByRole("alert")).toBeTruthy();
    expect(callbacks.onReversed).not.toHaveBeenCalled();
    expect(callbacks.onClose).not.toHaveBeenCalled();
  });
});
