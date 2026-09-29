// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { SupplierPaymentFormPage } from "./SupplierPaymentFormPage";
import { pendingPayablesFacade, type PendingInstallmentOption } from "../../payables/facades/pendingPayablesFacade";
import { supplierPaymentService } from "../api/supplierPaymentService";
import { paymentMethodLookupFacade, type PaymentMethodDto } from "../../sales/facades/paymentMethodLookupFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { cashFundingRequestFacade } from "../../caja/facades/cashFundingRequestFacade";

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return { ...actual, useNavigate: () => vi.fn() };
});

vi.mock("../../payables/facades/pendingPayablesFacade", () => ({
  pendingPayablesFacade: { listPendingInstallments: vi.fn() },
}));

vi.mock("../api/supplierPaymentService", () => ({
  supplierPaymentService: { register: vi.fn(), getPolicy: vi.fn() },
}));

vi.mock("../../sales/facades/paymentMethodLookupFacade", () => ({
  paymentMethodLookupFacade: { list: vi.fn() },
}));

// Mocks de otros módulos sin importarlos (el test no necesita sus tipos: solo su forma).
const external = vi.hoisted(() => ({
  getCashRegisters: vi.fn(),
  listBankAccounts: vi.fn(),
  getBusinessPartner: vi.fn(),
}));

vi.mock("../../finance/facades/bankAccountLookupFacade", () => ({
  bankAccountLookupFacade: { list: external.listBankAccounts },
}));

vi.mock("../../caja/facades/cashRegisterLookupFacade", () => ({ cashRegisterLookupFacade: { getCashRegisters: external.getCashRegisters } }));

vi.mock("../../caja/facades/cashFundingRequestFacade", () => ({
  cashFundingRequestFacade: { create: vi.fn() },
  cashFundingRequestRoute: (id: string) => `/treasury/cash/funding-requests/${id}`,
}));

vi.mock("../../masterData/facades/businessPartnerLookupFacade", () => ({
  businessPartnerLookupFacade: { getBusinessPartner: external.getBusinessPartner },
}));

vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

// SUPPLIER-PAYMENT-REMOVE-DUPLICATED-APPLICATION-LINES-FLOW-01 — SupplierSearchSelect es un componente
// de búsqueda async no relevante para este test; se sustituye por un botón que dispara la
// selección de un proveedor fijo (mismo patrón que ItemCodeCreateSections.test.tsx).
vi.mock("../../masterData/components/SupplierSearchSelect", () => ({
  SupplierSearchSelect: ({ onChange }: { onChange: (s: { id: string } | null) => void }) => (
    <button type="button" onClick={() => onChange({ id: "sup-1" })}>
      Seleccionar proveedor
    </button>
  ),
}));

function installment(over: Partial<PendingInstallmentOption> = {}): PendingInstallmentOption {
  return {
    installmentId: "inst-1",
    payableId: "pay-1",
    documentType: "FAC",
    documentNumber: "001-001-000031760",
    installmentNumber: 1,
    issueDate: "2026-08-01",
    dueDate: "2026-09-01",
    totalAmount: 100,
    paidAmount: 0,
    outstandingAmount: 43.53,
    status: "partiallypaid",
    ...over,
  };
}

const methods: PaymentMethodDto[] = [
  {
    id: "pm-1",
    code: "TRANSFER",
    name: "Transferencia",
    isActive: true,
    requiresReference: false,
    isCreditAllowed: false,
    sortOrder: 1,
    detailType: "Transfer",
    affectsPhysicalCash: false,
  } as PaymentMethodDto,
  {
    id: "pm-2",
    code: "CASH",
    name: "Efectivo",
    isActive: true,
    requiresReference: false,
    isCreditAllowed: false,
    sortOrder: 2,
    detailType: "None",
    affectsPhysicalCash: true,
  } as PaymentMethodDto,
];

const destinations = [
  {
    id: "fd-1",
    bankId: "bank-1",
    accountType: "Checking",
    accountNumber: "123",
    displayName: "Banco Principal",
    accountingAccountId: "acc-1",
    isActive: true,
  },
];

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <SupplierPaymentFormPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

async function selectSupplier() {
  fireEvent.click(await screen.findByText("Seleccionar proveedor"));
  await screen.findByText("Cartera pendiente del proveedor");
}

function mockAllowWithoutPayable(allow: boolean) {
  vi.mocked(supplierPaymentService.getPolicy).mockResolvedValue({ allowWithoutPayable: allow });
}

beforeEach(() => {
  mockAllowWithoutPayable(false);
  external.getCashRegisters.mockResolvedValue([{ id: "cash-1", name: "Caja Principal", isActive: true, accountingAccountId: "acc-2" }]);
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
  vi.mocked(paymentMethodLookupFacade.list).mockResolvedValue(methods);
  external.listBankAccounts.mockResolvedValue(destinations);
  external.getBusinessPartner.mockResolvedValue({ legalName: "Proveedor Test", tradeName: null });
  vi.mocked(supplierPaymentService.register).mockResolvedValue({
    id: "sp-1",
    displayNumber: "00000001",
  } as Awaited<ReturnType<typeof supplierPaymentService.register>>);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

// ── ZH-CASH-FUNDING-REQUEST-UI-FINAL-02E-EF — pago directo vs. solicitud de efectivo ──────────

const OWN_REGISTER = {
  id: "cash-1",
  name: "Caja Principal",
  isActive: true,
  accountingAccountId: "acc-2",
  hasOpenSession: true,
  openSessionControlledByCurrentUser: true,
  openSessionUserName: "Yo Mismo",
};

const FOREIGN_REGISTER = {
  id: "cash-9",
  name: "Caja Mostrador",
  isActive: true,
  accountingAccountId: "acc-3",
  hasOpenSession: true,
  openSessionControlledByCurrentUser: false,
  openSessionUserName: "Carla Cajera",
};

async function payInstallmentWith(lines: { method: string; destination: string; amount: string }[], applied: string) {
  vi.mocked(pendingPayablesFacade.listPendingInstallments).mockResolvedValue([installment({ outstandingAmount: 100 })]);
  renderPage();
  await selectSupplier();
  fireEvent.change(await screen.findByLabelText(/Monto a aplicar: FAC 001-001-000031760/), {
    target: { value: applied },
  });
  await screen.findAllByLabelText(/^Medio de pago\*$/);
  for (let i = 0; i < lines.length; i++) {
    if (i > 0) fireEvent.click(screen.getByText("+ Agregar medio de pago"));
    fireEvent.change(screen.getAllByLabelText(/^Medio de pago\*$/)[i], { target: { value: lines[i].method } });
    fireEvent.change(screen.getAllByLabelText(/^Caja \/ cuenta bancaria\*$/)[i], {
      target: { value: lines[i].destination },
    });
    fireEvent.change(screen.getAllByLabelText(/^Monto\*$/)[i], { target: { value: lines[i].amount } });
  }
}

describe("SupplierPaymentFormPage — solicitud de efectivo (02E-EF)", () => {
  beforeEach(() => {
    external.getCashRegisters.mockResolvedValue([OWN_REGISTER, FOREIGN_REGISTER]);
    vi.mocked(cashFundingRequestFacade.create).mockResolvedValue({
      id: "req-1",
    } as Awaited<ReturnType<typeof cashFundingRequestFacade.create>>);
  });

  it("10. efectivo de la caja propia sigue siendo pago directo", async () => {
    await payInstallmentWith([{ method: "pm-2", destination: "cash:cash-1", amount: "30" }], "30");

    expect(screen.queryByText("Solicitar efectivo")).toBeNull();
    fireEvent.click(screen.getByText("Pagar"));
    fireEvent.click(await screen.findByText("Confirmar y registrar"));

    await waitFor(() => expect(supplierPaymentService.register).toHaveBeenCalled());
    expect(cashFundingRequestFacade.create).not.toHaveBeenCalled();
  });

  it("11. pago solo banco sigue siendo pago directo", async () => {
    await payInstallmentWith([{ method: "pm-1", destination: "bank:fd-1", amount: "30" }], "30");

    fireEvent.click(screen.getByText("Pagar"));
    fireEvent.click(await screen.findByText("Confirmar y registrar"));

    await waitFor(() => expect(supplierPaymentService.register).toHaveBeenCalled());
    expect(cashFundingRequestFacade.create).not.toHaveBeenCalled();
  });

  it("12. efectivo de una caja ajena cambia el botón a Solicitar efectivo y explica quién la opera", async () => {
    await payInstallmentWith([{ method: "pm-2", destination: "cash:cash-9", amount: "30" }], "30");

    expect(await screen.findByText("Solicitar efectivo")).toBeTruthy();
    expect(screen.queryByText("Pagar")).toBeNull();
    expect(screen.getByText("La caja Caja Mostrador la opera Carla Cajera.")).toBeTruthy();
  });

  it("13–14. mixto banco + caja ajena crea UNA solicitud con el pago completo (nunca el pago) y enlaza a ella", async () => {
    await payInstallmentWith(
      [
        { method: "pm-1", destination: "bank:fd-1", amount: "70" },
        { method: "pm-2", destination: "cash:cash-9", amount: "30" },
      ],
      "100",
    );

    fireEvent.click(screen.getByText("Solicitar efectivo"));
    expect(await screen.findByText("Confirmar solicitud de efectivo")).toBeTruthy();
    fireEvent.click(screen.getAllByText("Solicitar efectivo").at(-1)!);

    await waitFor(() => expect(cashFundingRequestFacade.create).toHaveBeenCalledTimes(1));
    expect(supplierPaymentService.register).not.toHaveBeenCalled();
    const payload = vi.mocked(cashFundingRequestFacade.create).mock.calls[0][0];
    expect(payload.totalAmount).toBe(100);
    expect(payload.clientRequestId).toBeTruthy();
    expect(payload.methodLines).toEqual([
      expect.objectContaining({ companyBankAccountId: "fd-1", cashRegisterId: null, amount: 70 }),
      expect.objectContaining({ companyBankAccountId: null, cashRegisterId: "cash-9", amount: 30 }),
    ]);
    expect(payload.applicationLines).toEqual([{ accountsPayableInstallmentId: "inst-1", amountApplied: 100 }]);

    expect(await screen.findByText("Solicitud de efectivo creada")).toBeTruthy();
    expect(screen.getByText("Ver solicitud").getAttribute("href")).toBe("/treasury/cash/funding-requests/req-1");
    expect(document.body.textContent).not.toContain("req-1");
  });

  it("una solicitud no admite dos líneas de efectivo", async () => {
    await payInstallmentWith(
      [
        { method: "pm-2", destination: "cash:cash-9", amount: "20" },
        { method: "pm-2", destination: "cash:cash-1", amount: "10" },
      ],
      "30",
    );

    fireEvent.click(screen.getByText("Solicitar efectivo"));

    expect(
      await screen.findByText(
        "La solicitud de efectivo admite una sola línea de efectivo (una sola caja); las demás deben ser bancarias.",
      ),
    ).toBeTruthy();
    expect(cashFundingRequestFacade.create).not.toHaveBeenCalled();
    expect(supplierPaymentService.register).not.toHaveBeenCalled();
  });
});
