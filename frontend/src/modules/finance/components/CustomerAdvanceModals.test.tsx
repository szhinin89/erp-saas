// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { addDaysIso, todayIso } from "../../../lib/formatters/dateFormatters";
import { ADVANCE, receivable, resetCatalogs } from "../testing/customerAdvanceFixtures";

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => true }),
}));
vi.mock("../api/customerAdvanceService", () => ({
  customerAdvanceService: {
    list: vi.fn(),
    register: vi.fn(),
    apply: vi.fn(),
    refund: vi.fn(),
    reverseApplication: vi.fn(),
    reverseRefund: vi.fn(),
    reverseIncome: vi.fn(),
    cancel: vi.fn(),
  },
}));
vi.mock("../api/receivableService", () => ({ receivableService: { list: vi.fn() } }));
vi.mock("../api/bankAccountService", () => ({ bankAccountService: { list: vi.fn() } }));
vi.mock("../../sales/facades/paymentMethodLookupFacade", () => ({
  paymentMethodLookupFacade: { list: vi.fn() },
}));
vi.mock("../../caja/facades/cashRegisterLookupFacade", () => ({
  cashRegisterLookupFacade: { getCashRegisters: vi.fn() },
}));
vi.mock("../../session/facades/sessionBranchFacade", () => ({
  sessionBranchFacade: { listAvailable: vi.fn() },
}));
vi.mock("../../sales/facades/customerPickerFacade", () => ({
  CustomerPicker: ({ onChange }: { onChange: (c: { id: string }) => void }) => (
    <button type="button" onClick={() => onChange({ id: "cust-1" })}>
      elegir cliente
    </button>
  ),
}));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

import { message } from "../../../lib/messages";
import { customerAdvanceService } from "../api/customerAdvanceService";
import { receivableService } from "../api/receivableService";
import { ApplyCustomerAdvanceModal } from "./ApplyCustomerAdvanceModal";
import {
  CustomerAdvanceCompensationModal,
  type CompensationTarget,
} from "./CustomerAdvanceCompensationModal";
import { RefundCustomerAdvanceModal } from "./RefundCustomerAdvanceModal";
import { RegisterCustomerAdvanceModal } from "./RegisterCustomerAdvanceModal";

const field = (label: RegExp) => screen.getByLabelText(label) as HTMLInputElement;
const type = (label: RegExp, value: string) =>
  fireEvent.change(field(label), { target: { value } });
const button = (name: string) => screen.getByRole("button", { name });
const optionTexts = (label: RegExp) =>
  Array.from((field(label) as unknown as HTMLSelectElement).options).map((o) => o.textContent);
const wrap = (node: React.ReactNode) => render(<I18nProvider>{node}</I18nProvider>);

const httpError = (errors: unknown, userMessage = "No se puede completar la operación.") =>
  Object.assign(new Error("422"), {
    isAxiosError: true,
    response: { status: 422, data: { message: { user: userMessage }, data: { errors } } },
  });

beforeEach(() => {
  resetCatalogs();
  vi.mocked(message.confirm).mockResolvedValue(true);
  vi.mocked(receivableService.list).mockResolvedValue({
    items: [
      receivable({ id: "ar-1", invoiceNumber: "F-1", balanceDue: 60 }),
      receivable({ id: "ar-2", invoiceNumber: "F-2", balanceDue: 80 }),
    ],
    total: 2,
    page: 1,
    pageSize: 100,
  });
});
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

// ── Registro ─────────────────────────────────────────────────────────────

async function fillRegister(method: string, destination: string) {
  fireEvent.click(screen.getByText("elegir cliente"));
  await screen.findByRole("option", { name: "Efectivo" });
  type(/^Monto recibido/, "100");
  type(/^Medio de pago/, method);
  await waitFor(() => expect(optionTexts(/^(Caja|Cuenta bancaria)/).length).toBeGreaterThan(1));
  type(/^(Caja|Cuenta bancaria)/, destination);
}

function renderRegister(onRegistered = vi.fn()) {
  wrap(<RegisterCustomerAdvanceModal open onClose={vi.fn()} onRegistered={onRegistered} />);
  return onRegistered;
}

describe("RegisterCustomerAdvanceModal", () => {
  it("solo ofrece efectivo y transferencia, y el destino depende del medio", async () => {
    renderRegister();
    await screen.findByRole("option", { name: "Efectivo" });
    expect(optionTexts(/^Medio de pago/)).toEqual([
      "Seleccione un medio de pago",
      "Efectivo",
      "Transferencia",
    ]);
    expect(field(/^Destino/).disabled).toBe(true);

    type(/^Medio de pago/, "pm-cash");
    expect(optionTexts(/^Caja/)).toEqual(["Seleccione el destino", "Caja 1"]);
    type(/^Medio de pago/, "pm-transfer");
    expect(optionTexts(/^Cuenta bancaria/)).toEqual(["Seleccione el destino", "Banco Uno"]);
  });

  it.each([
    ["pm-cash", "cash:reg-1", { cashRegisterId: "reg-1", companyBankAccountId: null }],
    ["pm-transfer", "bank:bank-1", { cashRegisterId: null, companyBankAccountId: "bank-1" }],
  ])("registra con %s un destino excluyente y confirmado", async (method, destination, target) => {
    vi.mocked(customerAdvanceService.register).mockResolvedValue(ADVANCE);
    const onRegistered = renderRegister();
    await fillRegister(method, destination);

    fireEvent.click(button("Registrar anticipo"));

    await waitFor(() => expect(onRegistered).toHaveBeenCalledWith(ADVANCE));
    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(customerAdvanceService.register).toHaveBeenCalledWith({
      customerId: "cust-1",
      amount: 100,
      advanceDate: todayIso(),
      paymentMethodId: method,
      ...target,
      reference: null,
      reason: null,
      clientRequestId: expect.any(String),
    });
  });

  it("no envía sin destino, con fecha futura ni sin la confirmación", async () => {
    renderRegister();
    await fillRegister("pm-cash", "");
    type(/^Fecha del anticipo/, addDaysIso(todayIso(), 1));
    fireEvent.click(button("Registrar anticipo"));

    expect(await screen.findByText("Seleccione el destino.")).toBeTruthy();
    expect(screen.getByText("La fecha no puede ser posterior a hoy.")).toBeTruthy();
    expect(message.confirm).not.toHaveBeenCalled();

    type(/^Fecha del anticipo/, todayIso());
    type(/^Caja/, "cash:reg-1");
    vi.mocked(message.confirm).mockResolvedValue(false);
    fireEvent.click(button("Registrar anticipo"));
    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(customerAdvanceService.register).not.toHaveBeenCalled();
  });

  it("doble clic: una sola confirmación y un solo envío", async () => {
    let finish!: () => void;
    vi.mocked(customerAdvanceService.register).mockImplementation(
      () => new Promise((resolve) => (finish = () => resolve(ADVANCE))),
    );
    const onRegistered = renderRegister();
    await fillRegister("pm-cash", "cash:reg-1");

    const save = button("Registrar anticipo");
    fireEvent.click(save);
    fireEvent.click(save);
    await waitFor(() => expect(customerAdvanceService.register).toHaveBeenCalled());
    finish();

    await waitFor(() => expect(onRegistered).toHaveBeenCalled());
    expect(customerAdvanceService.register).toHaveBeenCalledTimes(1);
    expect(message.confirm).toHaveBeenCalledTimes(1);
  });

  it("reintentar conserva la clave; cambiar un dato crea otra intención", async () => {
    vi.mocked(customerAdvanceService.register).mockRejectedValue(new Error("sin conexión"));
    renderRegister();
    await fillRegister("pm-cash", "cash:reg-1");
    const send = async (times: number) => {
      fireEvent.click(await screen.findByRole("button", { name: "Registrar anticipo" }));
      await waitFor(() => expect(customerAdvanceService.register).toHaveBeenCalledTimes(times));
    };

    await send(1);
    await send(2);
    type(/^Monto recibido/, "101");
    await send(3);

    const keys = vi
      .mocked(customerAdvanceService.register)
      .mock.calls.map(([payload]) => payload.clientRequestId);
    expect(keys[1]).toBe(keys[0]);
    expect(keys[2]).not.toBe(keys[0]);
  });

  it("muestra los errores del servidor en su campo y los de dominio con su mensaje", async () => {
    vi.mocked(customerAdvanceService.register)
      .mockRejectedValueOnce(
        httpError({ amount: ["Monto rechazado"], companyBankAccountId: ["Cuenta inactiva"] }),
      )
      .mockRejectedValueOnce(httpError(["No existe una sesión de caja activa."]));
    renderRegister();
    await fillRegister("pm-transfer", "bank:bank-1");

    fireEvent.click(button("Registrar anticipo"));
    expect(await screen.findByText("Monto rechazado")).toBeTruthy();
    expect(screen.getByText("Cuenta inactiva")).toBeTruthy();

    type(/^Monto recibido/, "90");
    fireEvent.click(await screen.findByRole("button", { name: "Registrar anticipo" }));
    expect(await screen.findByText("No existe una sesión de caja activa.")).toBeTruthy();
  });
});

// ── Aplicación ───────────────────────────────────────────────────────────

describe("ApplyCustomerAdvanceModal", () => {
  const line = (index: number, label: RegExp) =>
    screen.getAllByLabelText(label)[index] as HTMLInputElement;
  const set = (element: HTMLElement, value: string) =>
    fireEvent.change(element, { target: { value } });

  it("desde el anticipo aplica a varias CxC del cliente en una operación", async () => {
    vi.mocked(customerAdvanceService.apply).mockResolvedValue(ADVANCE);
    const onApplied = vi.fn();
    wrap(
      <ApplyCustomerAdvanceModal open advance={ADVANCE} onClose={vi.fn()} onApplied={onApplied} />,
    );
    await screen.findByRole("option", { name: /F-1/ });
    // El aislamiento por cliente lo resuelve el servidor (ReceivableCustomerFilterApiTests).
    expect(receivableService.list).toHaveBeenCalledWith("pending", 1, 50, "cust-1");

    set(line(0, /^Cuenta por cobrar/), "ar-1");
    set(line(0, /^Monto a aplicar/), "60");
    fireEvent.click(button("Agregar otra cuenta por cobrar"));
    set(line(1, /^Cuenta por cobrar/), "ar-2");
    set(line(1, /^Monto a aplicar/), "50");
    fireEvent.click(button("Aplicar anticipo"));
    // 110 supera el disponible de 100.
    expect(await screen.findByText(/El monto no puede superar el saldo disponible/)).toBeTruthy();
    expect(customerAdvanceService.apply).not.toHaveBeenCalled();

    set(line(1, /^Monto a aplicar/), "40");
    fireEvent.click(button("Aplicar anticipo"));
    await waitFor(() => expect(onApplied).toHaveBeenCalled());
    expect(customerAdvanceService.apply).toHaveBeenCalledWith("adv-1", {
      effectiveDate: todayIso(),
      lines: [
        { receivableId: "ar-1", installmentId: null, amount: 60 },
        { receivableId: "ar-2", installmentId: null, amount: 40 },
      ],
      clientRequestId: expect.any(String),
    });
  });

  it("desde la CxC elige el anticipo y respeta saldo y fecha de origen", async () => {
    vi.mocked(customerAdvanceService.list).mockResolvedValue({
      items: [ADVANCE],
      total: 1,
      page: 1,
      pageSize: 100,
    });
    const target = receivable({ id: "ar-1", invoiceNumber: "F-1", balanceDue: 60 });
    wrap(
      <ApplyCustomerAdvanceModal open receivable={target} onClose={vi.fn()} onApplied={vi.fn()} />,
    );
    await screen.findByRole("option", { name: /REF-1/ });
    expect(customerAdvanceService.list).toHaveBeenCalledWith(1, 100, {
      customerId: "cust-1",
      status: "Confirmed",
    });
    expect(line(0, /^Cuenta por cobrar/).value).toBe("ar-1");

    set(field(/^Anticipo/), "adv-1");
    set(line(0, /^Monto a aplicar/), "61");
    set(field(/^Fecha efectiva/), addDaysIso(target.invoiceIssueDate, -1));
    fireEvent.click(button("Aplicar anticipo"));

    expect(await screen.findByText(/Supera el saldo pendiente de la cuenta/)).toBeTruthy();
    expect(screen.getByText(/La fecha no puede ser anterior al/)).toBeTruthy();
    expect(customerAdvanceService.apply).not.toHaveBeenCalled();
  });
});

// ── Devolución y compensaciones ──────────────────────────────────────────

describe("RefundCustomerAdvanceModal", () => {
  it("limita al disponible, exige evidencia y confirma antes de registrar", async () => {
    vi.mocked(customerAdvanceService.refund).mockResolvedValue(ADVANCE);
    const onRefunded = vi.fn();
    wrap(
      <RefundCustomerAdvanceModal
        open
        advance={ADVANCE}
        onClose={vi.fn()}
        onRefunded={onRefunded}
      />,
    );
    await screen.findByRole("option", { name: "Efectivo" });
    expect(screen.getByText(/El ERP no ejecuta transferencias bancarias/)).toBeTruthy();
    type(/^Monto a devolver/, "101");
    fireEvent.click(button("Registrar devolución"));
    expect(await screen.findByText(/El monto no puede superar el saldo disponible/)).toBeTruthy();
    expect(screen.getByText("La referencia es obligatoria.")).toBeTruthy();
    expect(screen.getByText("El motivo es obligatorio.")).toBeTruthy();

    type(/^Monto a devolver/, "30");
    type(/^Medio de pago/, "pm-transfer");
    type(/^Cuenta bancaria/, "bank:bank-1");
    type(/^Referencia de ejecución/, "TRX-1");
    type(/^Motivo/, "Devolución al cliente");
    fireEvent.click(button("Registrar devolución"));

    await waitFor(() => expect(onRefunded).toHaveBeenCalled());
    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(customerAdvanceService.refund).toHaveBeenCalledWith("adv-1", {
      amount: 30,
      effectiveDate: todayIso(),
      paymentMethodId: "pm-transfer",
      companyBankAccountId: "bank-1",
      cashRegisterId: null,
      reference: "TRX-1",
      reason: "Devolución al cliente",
      clientRequestId: expect.any(String),
    });
  });
});

describe("CustomerAdvanceCompensationModal", () => {
  const original = addDaysIso(todayIso(), -2);
  const open = (target: Partial<CompensationTarget> & Pick<CompensationTarget, "kind">) => {
    const onCompensated = vi.fn();
    wrap(
      <CustomerAdvanceCompensationModal
        advance={ADVANCE}
        target={{ targetId: "", amount: 100, originalDate: original, ...target }}
        onClose={vi.fn()}
        onCompensated={onCompensated}
      />,
    );
    return onCompensated;
  };
  const fill = () => {
    type(/^Motivo/, "Motivo");
    type(/^Referencia/, "ACTA-1");
  };

  it("la anulación exige la declaración, que no viene marcada", async () => {
    vi.mocked(customerAdvanceService.cancel).mockResolvedValue(ADVANCE);
    const onCompensated = open({ kind: "cancel" });
    const declaration = screen.getByRole("checkbox") as HTMLInputElement;
    expect(declaration.checked).toBe(false);
    expect(screen.getByText(/solo corresponde a dinero que nunca fue recibido/)).toBeTruthy();
    fill();
    fireEvent.click(button("Anular anticipo"));
    expect(await screen.findByText("Debe declarar que el dinero no fue recibido.")).toBeTruthy();
    expect(customerAdvanceService.cancel).not.toHaveBeenCalled();

    fireEvent.click(declaration);
    fireEvent.click(button("Anular anticipo"));
    await waitFor(() => expect(onCompensated).toHaveBeenCalled());
    expect(customerAdvanceService.cancel).toHaveBeenCalledWith("adv-1", {
      reason: "Motivo",
      reference: "ACTA-1",
      moneyNotReceived: true,
      clientRequestId: expect.any(String),
    });
  });

  it.each([
    ["reverseIncome", "Revertir ingreso"],
    ["reverseRefund", "Revertir devolución"],
  ] as const)(
    "%s valida la fecha de evidencia y no pide importe ni destino",
    async (kind, action) => {
      vi.mocked(customerAdvanceService[kind]).mockResolvedValue(ADVANCE);
      const onCompensated = open({ kind, targetId: "tx-1", destination: "Caja: Caja 1" });
      expect(screen.queryByLabelText(/^Monto/)).toBeNull();
      expect(screen.getByText("Monto de la operación")).toBeTruthy();
      expect(screen.queryByLabelText(/^(Caja|Cuenta bancaria|Destino)/)).toBeNull();
      expect(screen.getByText("Caja: Caja 1")).toBeTruthy();
      fill();

      for (const invalid of [addDaysIso(original, -1), addDaysIso(todayIso(), 1)]) {
        type(/^Fecha de la evidencia/, invalid);
        fireEvent.click(button(action));
        expect(await screen.findByText(/La fecha no puede ser/)).toBeTruthy();
      }
      expect(customerAdvanceService[kind]).not.toHaveBeenCalled();

      type(/^Fecha de la evidencia/, original);
      fireEvent.click(button(action));
      await waitFor(() => expect(onCompensated).toHaveBeenCalled());
      const body = {
        reason: "Motivo",
        reference: "ACTA-1",
        evidenceDate: original,
        clientRequestId: expect.any(String),
      };
      if (kind === "reverseRefund")
        expect(customerAdvanceService.reverseRefund).toHaveBeenCalledWith("adv-1", "tx-1", body);
      else expect(customerAdvanceService.reverseIncome).toHaveBeenCalledWith("adv-1", body);
    },
  );

  it("revierte la aplicación completa, mostrando sus CxC y el mensaje del servidor", async () => {
    vi.mocked(customerAdvanceService.reverseApplication).mockRejectedValue(
      httpError(["La cuenta por cobrar tiene cuotas que no cuadran con su monto cobrado."]),
    );
    open({
      kind: "reverseApplication",
      targetId: "op-1",
      amount: 70,
      affected: [
        { label: "F-1", amount: 40 },
        { label: "F-2", amount: 30 },
      ],
    });
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText("F-1")).toBeTruthy();
    expect(within(dialog).getByText("F-2")).toBeTruthy();
    expect(screen.queryByLabelText(/^Fecha de la evidencia/)).toBeNull();
    fill();
    fireEvent.click(button("Revertir aplicación"));

    expect(await screen.findByText(/cuotas que no cuadran/)).toBeTruthy();
    expect(customerAdvanceService.reverseApplication).toHaveBeenCalledWith("adv-1", "op-1", {
      reason: "Motivo",
      reference: "ACTA-1",
      clientRequestId: expect.any(String),
    });
  });
});
