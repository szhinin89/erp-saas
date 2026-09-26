import { cajaService, type CashRegisterDto } from "../../caja/api/cajaService";
// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { SupplierPaymentFormPage } from "./SupplierPaymentFormPage";
import { pendingPayablesFacade, type PendingInstallmentOption } from "../api/pendingPayablesFacade";
import { supplierPaymentService } from "../api/supplierPaymentService";
import { paymentMethodLookupFacade, type PaymentMethodDto } from "../../sales/facades/paymentMethodLookupFacade";
import {
  bankAccountService,
  type CompanyBankAccountDto,
} from "../../finance/api/bankAccountService";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return { ...actual, useNavigate: () => vi.fn() };
});

vi.mock("../api/pendingPayablesFacade", () => ({
  pendingPayablesFacade: { listPendingInstallments: vi.fn() },
}));

vi.mock("../api/supplierPaymentService", () => ({
  supplierPaymentService: { register: vi.fn() },
}));

vi.mock("../../sales/facades/paymentMethodLookupFacade", () => ({
  paymentMethodLookupFacade: { list: vi.fn() },
}));

vi.mock("../../finance/api/bankAccountService", () => ({
  bankAccountService: { list: vi.fn() },
}));

vi.mock("../../caja/api/cajaService", () => ({ cajaService: { getCashRegisters: vi.fn() } }));

vi.mock("../../masterData/api/businessPartnerFacade", () => ({
  businessPartnerFacade: { getBusinessPartner: vi.fn() },
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

const destinations: CompanyBankAccountDto[] = [
  {
    id: "fd-1",
    bankId: "bank-1",
    accountType: "Checking",
    accountNumber: "123",
    displayName: "Banco Principal",
    accountingAccountId: "acc-1",
    isActive: true,
  } as CompanyBankAccountDto,
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

async function fillMethodLine(paymentMethodId: string, amount: string) {
  fireEvent.change(await screen.findByLabelText(/^Medio de pago\*$/), {
    target: { value: paymentMethodId },
  });
  fireEvent.change(screen.getByLabelText(/^Caja \/ cuenta bancaria\*$/), {
    target: { value: "bank:fd-1" },
  });
  fireEvent.change(screen.getByLabelText(/^Monto\*$/), { target: { value: amount } });
}

function registerPayload() {
  return vi.mocked(supplierPaymentService.register).mock.calls.at(-1)?.[0];
}

beforeEach(() => {
  vi.mocked(cajaService.getCashRegisters).mockResolvedValue([{ id: "cash-1", name: "Caja Principal", isActive: true, accountingAccountId: "acc-2" } as CashRegisterDto]);
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
  vi.mocked(paymentMethodLookupFacade.list).mockResolvedValue(methods);
  vi.mocked(bankAccountService.list).mockResolvedValue(destinations);
  vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue({
    legalName: "Proveedor Test",
    tradeName: null,
  } as Awaited<ReturnType<typeof businessPartnerFacade.getBusinessPartner>>);
  vi.mocked(supplierPaymentService.register).mockResolvedValue({
    id: "sp-1",
    displayNumber: "00000001",
  } as Awaited<ReturnType<typeof supplierPaymentService.register>>);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("SupplierPaymentFormPage — cartera como única fuente de applicationLines", () => {
  it('no renderiza la sección "Cuotas a pagar" (flujo manual duplicado eliminado)', async () => {
    vi.mocked(pendingPayablesFacade.listPendingInstallments).mockResolvedValue([installment()]);
    renderPage();
    await selectSupplier();

    expect(screen.queryByText("Cuotas a pagar")).toBeNull();
    expect(screen.queryByText("+ Agregar cuota")).toBeNull();
  });

  it("no genera una applicationLine vacía por defecto al cargar la cartera", async () => {
    vi.mocked(pendingPayablesFacade.listPendingInstallments).mockResolvedValue([installment()]);
    renderPage();
    await selectSupplier();

    const amountInput = (await screen.findByLabelText(
      /Monto a aplicar: FAC 001-001-000031760/,
    )) as HTMLInputElement;
    expect(amountInput.value).toBe("");

    fireEvent.click(screen.getByText("Registrar pago"));
    expect(await screen.findByText("Debe seleccionar al menos una cuota.")).toBeTruthy();
    expect(supplierPaymentService.register).not.toHaveBeenCalled();
  });

  it("muestra empty state y bloquea el registro si el proveedor no tiene cartera pendiente", async () => {
    vi.mocked(pendingPayablesFacade.listPendingInstallments).mockResolvedValue([]);
    renderPage();
    await selectSupplier();

    expect(
      await screen.findByText("Este proveedor no tiene cuentas por pagar pendientes."),
    ).toBeTruthy();

    fireEvent.click(screen.getByText("Registrar pago"));
    expect(await screen.findByText("Debe seleccionar al menos una cuota.")).toBeTruthy();
    expect(supplierPaymentService.register).not.toHaveBeenCalled();
  });

  it("escribir 20.50 en la cartera crea una sola applicationLine válida y permite pago parcial", async () => {
    vi.mocked(pendingPayablesFacade.listPendingInstallments).mockResolvedValue([installment()]);
    renderPage();
    await selectSupplier();

    const amountInput = await screen.findByLabelText(/Monto a aplicar: FAC 001-001-000031760/);
    fireEvent.change(amountInput, { target: { value: "20.50" } });

    await fillMethodLine("pm-1", "20.50");
    fireEvent.click(screen.getByText("Registrar pago"));
    fireEvent.click(await screen.findByText("Confirmar y registrar"));

    await waitFor(() => expect(supplierPaymentService.register).toHaveBeenCalled());
    expect(registerPayload()?.applicationLines).toEqual([
      { accountsPayableInstallmentId: "inst-1", amountApplied: 20.5 },
    ]);
    expect(registerPayload()?.totalAmount).toBe(20.5);
  });

  it("borrar el monto de una cuota la quita limpiamente sin enviarla", async () => {
    vi.mocked(pendingPayablesFacade.listPendingInstallments).mockResolvedValue([installment()]);
    renderPage();
    await selectSupplier();

    const amountInput = await screen.findByLabelText(/Monto a aplicar: FAC 001-001-000031760/);
    fireEvent.change(amountInput, { target: { value: "30" } });
    fireEvent.change(amountInput, { target: { value: "" } });

    fireEvent.click(screen.getByText("Registrar pago"));
    expect(await screen.findByText("Debe seleccionar al menos una cuota.")).toBeTruthy();
    expect(supplierPaymentService.register).not.toHaveBeenCalled();
  });

  it('"Aplicar saldo completo" permite registrar el pago total de la cuota', async () => {
    vi.mocked(pendingPayablesFacade.listPendingInstallments).mockResolvedValue([
      installment({ outstandingAmount: 43.53 }),
    ]);
    renderPage();
    await selectSupplier();

    fireEvent.click(await screen.findByText("Aplicar saldo completo"));
    await fillMethodLine("pm-1", "43.53");
    fireEvent.click(screen.getByText("Registrar pago"));
    fireEvent.click(await screen.findByText("Confirmar y registrar"));

    await waitFor(() => expect(supplierPaymentService.register).toHaveBeenCalled());
    expect(registerPayload()?.applicationLines).toEqual([
      { accountsPayableInstallmentId: "inst-1", amountApplied: 43.53 },
    ]);
  });

  it("permite pagar múltiples cuotas del mismo proveedor con múltiples medios de pago", async () => {
    vi.mocked(pendingPayablesFacade.listPendingInstallments).mockResolvedValue([
      installment({ installmentId: "inst-1", documentNumber: "001-001-000031760", outstandingAmount: 40 }),
      installment({
        installmentId: "inst-2",
        documentNumber: "001-001-000031761",
        installmentNumber: 2,
        outstandingAmount: 60,
      }),
    ]);
    renderPage();
    await selectSupplier();

    fireEvent.change(await screen.findByLabelText(/Monto a aplicar: FAC 001-001-000031760/), {
      target: { value: "40" },
    });
    fireEvent.change(await screen.findByLabelText(/Monto a aplicar: FAC 001-001-000031761/), {
      target: { value: "60" },
    });

    await fillMethodLine("pm-1", "70");
    fireEvent.click(screen.getByText("+ Agregar medio de pago"));
    const financialSelects = screen.getAllByLabelText(/^Caja \/ cuenta bancaria\*$/);
    const methodSelects = screen.getAllByLabelText(/^Medio de pago\*$/);
    const amountInputs = screen.getAllByLabelText(/^Monto\*$/);
    fireEvent.change(methodSelects[1], { target: { value: "pm-2" } });
    fireEvent.change(financialSelects[1], { target: { value: "cash:cash-1" } });
    fireEvent.change(amountInputs[1], { target: { value: "30" } });

    fireEvent.click(screen.getByText("Registrar pago"));
    fireEvent.click(await screen.findByText("Confirmar y registrar"));

    await waitFor(() => expect(supplierPaymentService.register).toHaveBeenCalled());
    const payload = registerPayload();
    expect(payload?.applicationLines).toEqual(
      expect.arrayContaining([
        { accountsPayableInstallmentId: "inst-1", amountApplied: 40 },
        { accountsPayableInstallmentId: "inst-2", amountApplied: 60 },
      ]),
    );
    expect(payload?.applicationLines).toHaveLength(2);
    expect(payload?.totalAmount).toBe(100);
    expect(payload?.methodLines).toEqual([
      expect.objectContaining({ companyBankAccountId: "fd-1", cashRegisterId: null, amount: 70 }),
      expect.objectContaining({ companyBankAccountId: null, cashRegisterId: "cash-1", amount: 30 }),
    ]);
  });

  it("no permite registrar si la suma de medios de pago no cuadra con el total aplicado", async () => {
    vi.mocked(pendingPayablesFacade.listPendingInstallments).mockResolvedValue([installment()]);
    renderPage();
    await selectSupplier();

    fireEvent.change(await screen.findByLabelText(/Monto a aplicar: FAC 001-001-000031760/), {
      target: { value: "20.50" },
    });
    await fillMethodLine("pm-1", "15");

    fireEvent.click(screen.getByText("Registrar pago"));

    // La validación Zod (superRefine: Σmedios === Σcuotas) bloquea el submit — deja tiempo a que
    // el resolver async resuelva y luego confirma que el modal de confirmación nunca se abre y
    // el backend nunca se llama con montos descuadrados.
    await new Promise((resolve) => setTimeout(resolve, 100));
    expect(screen.queryByText("Confirmar y registrar")).toBeNull();
    expect(supplierPaymentService.register).not.toHaveBeenCalled();
  });
});

describe("SupplierPaymentFormPage — 02A medio ↔ destino y datos bancarios", () => {
  const creditMethod = {
    id: "pm-credit",
    code: "CREDITO",
    name: "Crédito",
    isActive: true,
    requiresReference: false,
    isCreditAllowed: true,
    sortOrder: 5,
    detailType: "None",
    affectsPhysicalCash: false,
  } as PaymentMethodDto;
  const referencedTransfer = {
    id: "pm-ref",
    code: "TRANSFERENCIA",
    name: "Transferencia Bancaria",
    isActive: true,
    requiresReference: true,
    isCreditAllowed: false,
    sortOrder: 3,
    detailType: "Transfer",
    affectsPhysicalCash: false,
  } as PaymentMethodDto;

  beforeEach(() => {
    vi.mocked(paymentMethodLookupFacade.list).mockResolvedValue([...methods, creditMethod, referencedTransfer]);
    vi.mocked(pendingPayablesFacade.listPendingInstallments).mockResolvedValue([installment()]);
  });

  function destinationValues(): string[] {
    const select = screen.getByLabelText(/^Caja \/ cuenta bancaria\*$/) as HTMLSelectElement;
    return Array.from(select.options).map((o) => o.value).filter(Boolean);
  }

  it("un medio de efectivo solo ofrece cajas y uno bancario solo cuentas bancarias", async () => {
    renderPage();
    await selectSupplier();
    const methodSelect = await screen.findByLabelText(/^Medio de pago\*$/);

    fireEvent.change(methodSelect, { target: { value: "pm-2" } });
    await waitFor(() => expect(destinationValues()).toEqual(["cash:cash-1"]));

    fireEvent.change(methodSelect, { target: { value: "pm-1" } });
    await waitFor(() => expect(destinationValues()).toEqual(["bank:fd-1"]));
  });

  it("no ofrece medios de crédito para pagar a un proveedor", async () => {
    renderPage();
    await selectSupplier();
    const methodSelect = (await screen.findByLabelText(/^Medio de pago\*$/)) as HTMLSelectElement;

    await waitFor(() => expect(Array.from(methodSelect.options).map((o) => o.value)).toContain("pm-ref"));
    expect(Array.from(methodSelect.options).map((o) => o.value)).not.toContain("pm-credit");
  });

  it("envía la fecha de transacción bancaria informada y el número de operación", async () => {
    renderPage();
    await selectSupplier();
    fireEvent.change(await screen.findByLabelText(/Monto a aplicar: FAC 001-001-000031760/), {
      target: { value: "20" },
    });
    await fillMethodLine("pm-ref", "20");
    fireEvent.change(screen.getByLabelText(/^Fecha de transacción bancaria\*$/), {
      target: { value: "2026-08-26" },
    });
    fireEvent.change(screen.getByLabelText(/^Número de operación bancaria\*$/), {
      target: { value: "000987654" },
    });

    fireEvent.click(screen.getByText("Registrar pago"));
    fireEvent.click(await screen.findByText("Confirmar y registrar"));

    await waitFor(() => expect(supplierPaymentService.register).toHaveBeenCalled());
    expect(registerPayload()?.methodLines).toEqual([
      expect.objectContaining({
        companyBankAccountId: "fd-1",
        cashRegisterId: null,
        amount: 20,
        referenceNumber: "000987654",
        transactionDate: "2026-08-26",
      }),
    ]);
  });

  it("precarga visiblemente la fecha del pago en la fuente bancaria y la envía explícita; efectivo nunca envía fecha bancaria", async () => {
    vi.mocked(pendingPayablesFacade.listPendingInstallments).mockResolvedValue([
      installment({ outstandingAmount: 100 }),
    ]);
    renderPage();
    await selectSupplier();
    fireEvent.change(await screen.findByLabelText(/Monto a aplicar: FAC 001-001-000031760/), {
      target: { value: "100" },
    });
    await fillMethodLine("pm-1", "70");
    const bankDateInput = screen.getByLabelText(/^Fecha de transacción bancaria\*$/) as HTMLInputElement;
    await waitFor(() => expect(bankDateInput.value).not.toBe(""));
    fireEvent.click(screen.getByText("+ Agregar medio de pago"));
    fireEvent.change(screen.getAllByLabelText(/^Medio de pago\*$/)[1], { target: { value: "pm-2" } });
    fireEvent.change(screen.getAllByLabelText(/^Caja \/ cuenta bancaria\*$/)[1], {
      target: { value: "cash:cash-1" },
    });
    fireEvent.change(screen.getAllByLabelText(/^Monto\*$/)[1], { target: { value: "30" } });

    fireEvent.click(screen.getByText("Registrar pago"));
    fireEvent.click(await screen.findByText("Confirmar y registrar"));

    await waitFor(() => expect(supplierPaymentService.register).toHaveBeenCalled());
    const payload = registerPayload();
    expect(payload?.methodLines[0].transactionDate).toBe(payload?.paymentDate);
    expect(payload?.methodLines[1].transactionDate).toBeNull();
  });

  it("si el usuario borra la fecha bancaria precargada, bloquea el registro (nunca se completa sola)", async () => {
    renderPage();
    await selectSupplier();
    fireEvent.change(await screen.findByLabelText(/Monto a aplicar: FAC 001-001-000031760/), {
      target: { value: "20" },
    });
    await fillMethodLine("pm-1", "20");
    const bankDateInput = screen.getByLabelText(/^Fecha de transacción bancaria\*$/) as HTMLInputElement;
    await waitFor(() => expect(bankDateInput.value).not.toBe(""));
    fireEvent.change(bankDateInput, { target: { value: "" } });

    fireEvent.click(screen.getByText("Registrar pago"));

    expect(await screen.findByText("La fecha de la transacción bancaria es obligatoria.")).toBeTruthy();
    expect(bankDateInput.value).toBe("");
    expect(supplierPaymentService.register).not.toHaveBeenCalled();
  });

  it("bloquea el registro si el medio exige número de operación y no se informa", async () => {
    renderPage();
    await selectSupplier();
    fireEvent.change(await screen.findByLabelText(/Monto a aplicar: FAC 001-001-000031760/), {
      target: { value: "20" },
    });
    await fillMethodLine("pm-ref", "20");

    fireEvent.click(screen.getByText("Registrar pago"));

    expect(
      await screen.findByText("El número de operación bancaria es obligatorio para este medio de pago."),
    ).toBeTruthy();
    expect(supplierPaymentService.register).not.toHaveBeenCalled();
  });
});
