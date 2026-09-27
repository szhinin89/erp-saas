// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { CashFundingRequestDetailPage } from "./CashFundingRequestDetailPage";
import { cashFundingRequestService, type CashFundingRequestDto } from "../api/cashFundingRequestService";
import { message } from "../../../lib/messages";

vi.mock("../api/cashFundingRequestService", () => ({
  cashFundingRequestService: { getById: vi.fn(), fulfill: vi.fn(), reject: vi.fn(), cancel: vi.fn() },
}));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn(), prompt: vi.fn() },
}));

const ID = "3f1c2d4e-0000-4000-9000-00000000000a";
const g = (n: number) => `3f1c2d4e-0000-4000-9000-${String(n).padStart(12, "0")}`;

const REQUEST: CashFundingRequestDto = {
  id: ID,
  status: "Pending",
  branchId: g(1),
  branchName: "Matriz",
  cashRegisterId: g(2),
  cashRegisterName: "Caja Principal",
  cashSessionId: g(3),
  supplierId: g(4),
  supplierName: "Distribuidora Andina",
  totalAmount: 200,
  cashAmount: 80,
  requestedByUserId: g(5),
  requestedByName: "Sergio Compras",
  requestedAtUtc: "2026-09-26T15:00:00Z",
  resolvedByUserId: null,
  resolvedByName: null,
  resolvedAtUtc: null,
  resolutionReason: null,
  supplierPaymentId: null,
  paymentDate: "2026-09-26",
  receiptNumber: null,
  sources: [
    {
      kind: "Bank",
      paymentMethodId: g(6),
      paymentMethodName: "Transferencia",
      cashRegisterId: null,
      cashRegisterName: null,
      companyBankAccountId: g(7),
      bankAccountName: "Banco Pichincha CTE",
      amount: 120,
      transactionDate: "2026-09-26",
      referenceNumber: "OP-7788",
      checkNumber: null,
    },
    {
      kind: "Cash",
      paymentMethodId: g(8),
      paymentMethodName: "Efectivo",
      cashRegisterId: g(2),
      cashRegisterName: "Caja Principal",
      companyBankAccountId: null,
      bankAccountName: null,
      amount: 80,
      transactionDate: null,
      referenceNumber: null,
      checkNumber: null,
    },
  ],
  applications: [
    {
      accountsPayableInstallmentId: g(9),
      accountsPayableId: g(10),
      documentNumber: "001-001-000000123",
      originType: "PurchaseInvoice",
      installmentNumber: 1,
      amountApplied: 150,
    },
    {
      accountsPayableInstallmentId: g(11),
      accountsPayableId: g(12),
      documentNumber: "GASTO-0007",
      originType: "ExpenseDocument",
      installmentNumber: 2,
      amountApplied: 50,
    },
  ],
  canFulfill: false,
  canReject: false,
  canCancel: false,
};

const renderPage = () =>
  render(
    <MemoryRouter initialEntries={[`/treasury/cash/funding-requests/${ID}`]}>
      <Routes>
        <Route path="/treasury/cash/funding-requests/:id" element={<CashFundingRequestDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );

const withFlags = (flags: Partial<CashFundingRequestDto>) =>
  vi.mocked(cashFundingRequestService.getById).mockResolvedValue({ ...REQUEST, ...flags });

beforeEach(() => withFlags({}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("CashFundingRequestDetailPage", () => {
  it("5. muestra resumen, origen banco + caja y aplicaciones a CxP (Compra/Gasto)", async () => {
    renderPage();

    expect(await screen.findByText("Sergio Compras")).toBeTruthy();
    expect(screen.getByText("Matriz")).toBeTruthy();
    expect(screen.getByText("Banco · Banco Pichincha CTE")).toBeTruthy();
    expect(screen.getByText("Transferencia")).toBeTruthy();
    expect(screen.getByText("OP-7788")).toBeTruthy();
    expect(screen.getByText("Caja · Caja Principal")).toBeTruthy();
    expect(screen.getByText("Compra")).toBeTruthy();
    expect(screen.getByText("Gasto")).toBeTruthy();
    expect(screen.getByText("001-001-000000123").getAttribute("href")).toBe(`/payables/${g(10)}`);
    expect(screen.getByText("Cuota 2")).toBeTruthy();
  });

  it("sin flags del servidor no hay acciones", async () => {
    renderPage();
    await screen.findByText("Sergio Compras");
    expect(screen.queryByText("Entregar efectivo")).toBeNull();
    expect(screen.queryByText("Rechazar")).toBeNull();
    expect(screen.queryByText("Cancelar solicitud")).toBeNull();
  });

  it("6/9. CanFulfill muestra Entregar; confirma el monto y enlaza al pago generado", async () => {
    withFlags({ canFulfill: true, canReject: true });
    vi.mocked(message.confirm).mockResolvedValue(true);
    vi.mocked(cashFundingRequestService.fulfill).mockResolvedValue({
      ...REQUEST,
      status: "Fulfilled",
      resolvedByName: "Carla Cajera",
      resolvedAtUtc: "2026-09-26T16:00:00Z",
      supplierPaymentId: g(20),
    });
    renderPage();

    fireEvent.click(await screen.findByText("Entregar efectivo"));
    await waitFor(() => expect(cashFundingRequestService.fulfill).toHaveBeenCalledWith(ID));
    expect(String(vi.mocked(message.confirm).mock.calls[0][0].message)).toMatch(
      /^Confirmo que estoy entregando \$?80\.00 en efectivo para completar este pago\.$/,
    );
    const link = await screen.findByText("Ver pago generado");
    expect(link.getAttribute("href")).toBe(`/supplier-payments/${g(20)}`);
    expect(screen.getByText("Carla Cajera")).toBeTruthy();
    expect(screen.queryByText("Entregar efectivo")).toBeNull();
  });

  it("7. CanReject muestra Rechazar y exige motivo", async () => {
    withFlags({ canReject: true });
    vi.mocked(message.prompt).mockResolvedValueOnce("  ").mockResolvedValueOnce("Sin efectivo");
    vi.mocked(cashFundingRequestService.reject).mockResolvedValue({ ...REQUEST, status: "Rejected" });
    renderPage();

    fireEvent.click(await screen.findByText("Rechazar"));
    await waitFor(() => expect(message.prompt).toHaveBeenCalledTimes(1));
    expect(cashFundingRequestService.reject).not.toHaveBeenCalled();
    expect(screen.queryByText("Entregar efectivo")).toBeNull();

    fireEvent.click(screen.getByText("Rechazar"));
    await waitFor(() => expect(cashFundingRequestService.reject).toHaveBeenCalledWith(ID, "Sin efectivo"));
  });

  it("8. CanCancel muestra Cancelar solicitud con motivo obligatorio", async () => {
    withFlags({ canCancel: true });
    vi.mocked(message.prompt).mockResolvedValue("Ya no se necesita");
    vi.mocked(cashFundingRequestService.cancel).mockResolvedValue({ ...REQUEST, status: "Cancelled" });
    renderPage();

    fireEvent.click(await screen.findByText("Cancelar solicitud"));
    await waitFor(() => expect(cashFundingRequestService.cancel).toHaveBeenCalledWith(ID, "Ya no se necesita"));
    expect(vi.mocked(message.prompt).mock.calls[0][0].required).toBe(true);
    expect(screen.queryByText("Rechazar")).toBeNull();
  });

  it("16. ningún GUID ni dato técnico visible", async () => {
    withFlags({ status: "Fulfilled", supplierPaymentId: g(20), resolvedByName: "Carla Cajera" });
    renderPage();
    await screen.findByText("Sergio Compras");
    const text = document.body.textContent ?? "";
    expect(text).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}-/i);
    expect(text).not.toMatch(/payload|hash|clientRequestId/i);
  });
});
