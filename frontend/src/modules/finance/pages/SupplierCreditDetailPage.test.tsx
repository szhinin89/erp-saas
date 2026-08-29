// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { SupplierCreditDetailPage } from "./SupplierCreditDetailPage";
import {
  supplierCreditService,
  type SupplierCreditDto,
} from "../api/supplierCreditService";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-CLEANUP-07 — residuo encontrado en el barrido: "Revertir reembolso"
 * usaba window.prompt (diálogo nativo prohibido) para pedir el motivo. Se reemplaza por
 * message.prompt. No cambia el payload enviado a reverseRefund (reason/effectiveDate/
 * clientRequestId), ni la lógica de reversa.
 */

vi.mock("react-router-dom", () => ({
  useParams: () => ({ id: "credit-1" }),
  useNavigate: () => vi.fn(),
}));

vi.mock("../api/supplierCreditService", () => ({
  supplierCreditService: {
    getById: vi.fn(),
    apply: vi.fn(),
    reverseApplication: vi.fn(),
    registerRefund: vi.fn(),
    reverseRefund: vi.fn(),
  },
}));

vi.mock("../components/ApplySupplierCreditModal", () => ({
  ApplySupplierCreditModal: () => null,
}));

vi.mock("../components/RegisterSupplierCreditRefundModal", () => ({
  RegisterSupplierCreditRefundModal: () => null,
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
    prompt: vi.fn(),
  },
}));

const CREDIT: SupplierCreditDto = {
  id: "credit-1",
  supplierId: "supplier-1",
  branchId: "branch-1",
  currencyCode: "USD",
  sourcePurchaseReturnId: "return-1",
  originalAmount: 100,
  availableAmount: 60,
  isOpen: true,
  movements: [
    {
      id: "mov-refund-1",
      movementType: "Refund",
      amount: 40,
      targetPurchasePayableId: null,
      reversalOfMovementId: null,
      createdAtUtc: "2026-08-01T00:00:00Z",
    },
  ],
};

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(supplierCreditService.getById).mockResolvedValue(CREDIT);
});

describe("SupplierCreditDetailPage — revertir reembolso: sin window.prompt", () => {
  it("usa message.prompt en vez de window.prompt para pedir el motivo", async () => {
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("motivo nativo");
    vi.mocked(message.prompt).mockResolvedValue("Reembolso duplicado por error.");
    vi.mocked(supplierCreditService.reverseRefund).mockResolvedValue({
      id: "tx-1",
      transactionTypeCode: "RefundReversal",
      originalTransactionId: null,
      financialDestinationId: "dest-1",
      accountingAccountId: "acc-1",
      paymentMethodCode: "CASH",
      amount: 40,
      currencyCode: "USD",
      effectiveDate: "2026-08-28",
      externalReference: null,
      reason: "Reembolso duplicado por error.",
      cashSessionId: null,
      cashMovementId: null,
    });

    render(<SupplierCreditDetailPage />);
    await waitFor(() => expect(screen.getByText("Revertir")).toBeTruthy());

    fireEvent.click(screen.getByText("Revertir"));

    await waitFor(() => expect(message.prompt).toHaveBeenCalledTimes(1));
    expect(promptSpy).not.toHaveBeenCalled();
    await waitFor(() =>
      expect(supplierCreditService.reverseRefund).toHaveBeenCalledWith(
        "credit-1",
        "mov-refund-1",
        expect.objectContaining({ reason: "Reembolso duplicado por error." }),
      ),
    );
    expect(message.success).toHaveBeenCalledWith("Reembolso revertido correctamente.");

    promptSpy.mockRestore();
  });

  it("si se cancela el prompt (null), no llama a reverseRefund", async () => {
    vi.mocked(message.prompt).mockResolvedValue(null);

    render(<SupplierCreditDetailPage />);
    await waitFor(() => expect(screen.getByText("Revertir")).toBeTruthy());

    fireEvent.click(screen.getByText("Revertir"));

    await waitFor(() => expect(message.prompt).toHaveBeenCalled());
    expect(supplierCreditService.reverseRefund).not.toHaveBeenCalled();
  });
});
