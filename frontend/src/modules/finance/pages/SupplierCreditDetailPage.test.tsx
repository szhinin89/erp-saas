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
  Link: ({ to, children, className }: { to: string; children: React.ReactNode; className?: string }) => (
    <a href={to} className={className}>
      {children}
    </a>
  ),
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
  supplierName: "Proveedor Test",
  branchId: "branch-1",
  currencyCode: "USD",
  sourceType: "PurchaseReturn",
  sourceDocumentId: "return-1",
  sourcePurchaseReturnId: "return-1",
  sourceSupplierPaymentId: null,
  sourceDocumentNumber: "00000003",
  sourceDate: "2026-08-01",
  originalAmount: 100,
  availableAmount: 60,
  isOpen: true,
  movements: [
    {
      id: "mov-refund-1",
      movementType: "Refund",
      amount: 40,
      createdAtUtc: "2026-08-01T00:00:00Z",
      createdByUserId: "user-1",
      createdByName: "Ana Tesorera",
      reversalOfMovementId: null,
      reversedByMovementId: null,
      accountsPayableId: null,
      payableDocumentNumber: null,
      payableOriginType: null,
      refundTransactionId: "refund-tx-1",
      effectiveDate: "2026-08-01",
      destinationType: "Bank",
      destinationName: "Banco Pichincha CTE",
      paymentMethodCode: "TRANSFER",
      paymentMethodName: "Transferencia",
      referenceNumber: "TRX-1",
      reason: null,
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
      companyBankAccountId: "dest-1",
      cashRegisterId: null,
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

// ══════════════════════════════════════════════════════════════════════
// ZH-SUPPLIER-BALANCES-UX-02D-E — resumen, acciones e historial enriquecido
// ══════════════════════════════════════════════════════════════════════

const APPLICATION_MOVEMENT = {
  ...CREDIT.movements[0]!,
  id: "mov-app-1",
  movementType: "Application",
  amount: 15,
  accountsPayableId: "payable-9",
  payableDocumentNumber: "GAS-000777",
  payableOriginType: "ExpenseDocument",
  refundTransactionId: null,
  effectiveDate: null,
  destinationType: null,
  destinationName: null,
  paymentMethodCode: null,
  paymentMethodName: null,
  referenceNumber: null,
};

describe("SupplierCreditDetailPage — 02D-E", () => {
  it("resumen muestra proveedor, origen con enlace y el saldo disponible del servidor", async () => {
    render(<SupplierCreditDetailPage />);
    await waitFor(() => expect(screen.getByText("Resumen")).toBeTruthy());
    expect(screen.getAllByText("Proveedor Test").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Devolución de compra/).length).toBeGreaterThan(0);
    expect(screen.getByText("00000003").closest("a")!.getAttribute("href")).toBe("/purchases/returns/return-1");
    expect(screen.getByText("Saldo disponible")).toBeTruthy();
    expect(screen.getByText("60.00")).toBeTruthy();
    expect(screen.getByText("Aplicar a CxP")).toBeTruthy();
    expect(screen.getByText("Registrar reembolso")).toBeTruthy();
  });

  it("historial: la aplicación muestra Compra/Gasto con enlace a la CxP", async () => {
    vi.mocked(supplierCreditService.getById).mockResolvedValue({
      ...CREDIT,
      movements: [APPLICATION_MOVEMENT],
    });
    render(<SupplierCreditDetailPage />);
    await waitFor(() => expect(screen.getByText("Gasto · GAS-000777")).toBeTruthy());
    expect(screen.getByText("Gasto · GAS-000777").closest("a")!.getAttribute("href")).toBe("/payables/payable-9");
    expect(screen.getByText("Aplicación a CxP")).toBeTruthy();
    expect(screen.getAllByText("Ana Tesorera").length).toBeGreaterThan(0);
  });

  it("historial: el reembolso muestra banco, destino, medio legible y referencia (no el código)", async () => {
    render(<SupplierCreditDetailPage />);
    await waitFor(() => expect(screen.getByText("Banco · Banco Pichincha CTE")).toBeTruthy());
    expect(screen.getByText("Transferencia")).toBeTruthy();
    expect(screen.queryByText("TRANSFER")).toBeNull();
    expect(screen.getByText("Ref. TRX-1")).toBeTruthy();
  });

  it("la reversa indica qué movimiento revierte y su motivo; el original ya no ofrece revertir", async () => {
    vi.mocked(supplierCreditService.getById).mockResolvedValue({
      ...CREDIT,
      movements: [
        { ...CREDIT.movements[0]!, reversedByMovementId: "mov-rev-1" },
        {
          ...CREDIT.movements[0]!,
          id: "mov-rev-1",
          movementType: "ReversalOfRefund",
          reversalOfMovementId: "mov-refund-1",
          referenceNumber: null,
          reason: "Transferencia devuelta",
          createdAtUtc: "2026-08-02T00:00:00Z",
        },
      ],
    });
    render(<SupplierCreditDetailPage />);
    await waitFor(() => expect(screen.getByText("Motivo: Transferencia devuelta")).toBeTruthy());
    expect(screen.getByText(/Revierte: Reembolso del/)).toBeTruthy();
    expect(screen.getByText("Revertido")).toBeTruthy();
    expect(screen.queryByText("Revertir")).toBeNull();
  });

  it("sin saldo disponible no ofrece Aplicar ni Registrar reembolso", async () => {
    vi.mocked(supplierCreditService.getById).mockResolvedValue({ ...CREDIT, availableAmount: 0, isOpen: false });
    render(<SupplierCreditDetailPage />);
    await waitFor(() => expect(screen.getByText("Resumen")).toBeTruthy());
    expect(screen.queryByText("Aplicar a CxP")).toBeNull();
    expect(screen.queryByText("Registrar reembolso")).toBeNull();
    expect(screen.getByText("Cerrado")).toBeTruthy();
  });
});
