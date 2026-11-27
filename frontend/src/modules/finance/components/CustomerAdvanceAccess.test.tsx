// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ADVANCE, receivable, resetCatalogs } from "../testing/customerAdvanceFixtures";

const granted = new Set<string>();

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: (key: string) => granted.has(key) }),
}));
vi.mock("../api/customerAdvanceService", () => ({
  customerAdvanceService: { list: vi.fn(), register: vi.fn(), apply: vi.fn() },
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
import { bankAccountService } from "../api/bankAccountService";
import { customerAdvanceService } from "../api/customerAdvanceService";
import { receivableService } from "../api/receivableService";
import { cashRegisterLookupFacade } from "../../caja/facades/cashRegisterLookupFacade";
import { ApplyCustomerAdvanceModal } from "./ApplyCustomerAdvanceModal";
import { RegisterCustomerAdvanceModal } from "./RegisterCustomerAdvanceModal";

const CUSTOMERS = "masterdata.businesspartners.view";
const BANKS = "treasury.banks.accounts.view";
const REGISTERS = "caja.view";
const SALES = "sales.view";

const wrap = (node: React.ReactNode) => render(<I18nProvider>{node}</I18nProvider>);
const field = (label: RegExp) => screen.getByLabelText(label) as HTMLSelectElement;
const set = (label: RegExp, value: string) => fireEvent.change(field(label), { target: { value } });
const page = (from: number, count: number, total: number) => ({
  items: Array.from({ length: count }, (_, i) =>
    receivable({ id: `ar-${from + i}`, invoiceNumber: `F-${from + i}` }),
  ),
  total,
  page: 1,
  pageSize: 50,
});

beforeEach(() => {
  granted.clear();
  resetCatalogs();
  vi.mocked(message.confirm).mockResolvedValue(true);
});
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

async function openRegister(method: string) {
  wrap(<RegisterCustomerAdvanceModal open onClose={vi.fn()} onRegistered={vi.fn()} />);
  await screen.findByRole("option", { name: "Efectivo" });
  set(/^Medio de pago/, method);
}

describe("Registro — cada recurso exige solo su permiso", () => {
  it.each([
    ["pm-cash", REGISTERS, /^Caja/, "cash:reg-1"],
    ["pm-transfer", BANKS, /^Cuenta bancaria/, "bank:bank-1"],
  ])("%s funciona sin el permiso del otro destino", async (method, own, label, destination) => {
    granted.add(CUSTOMERS).add(own);
    vi.mocked(customerAdvanceService.register).mockResolvedValue(ADVANCE);
    await openRegister(method);

    await waitFor(() => expect(field(label).options.length).toBeGreaterThan(1));
    expect(screen.queryByText(/No tiene permiso/)).toBeNull();
    fireEvent.click(screen.getByText("elegir cliente"));
    set(/^Monto recibido/, "10");
    set(label, destination);
    fireEvent.click(screen.getByRole("button", { name: "Registrar anticipo" }));

    await waitFor(() => expect(customerAdvanceService.register).toHaveBeenCalled());
    const other =
      own === BANKS ? cashRegisterLookupFacade.getCashRegisters : bankAccountService.list;
    expect(other).not.toHaveBeenCalled();
  });

  it.each([
    ["pm-cash", /No tiene permiso para consultar cajas \(caja\.view\)/, /^Caja/],
    [
      "pm-transfer",
      /No tiene permiso para consultar cuentas bancarias \(treasury\.banks\.accounts\.view\)/,
      /^Cuenta bancaria/,
    ],
  ])("%s sin su permiso lo explica y no aparenta estar vacío", async (method, notice, label) => {
    granted.add(CUSTOMERS);
    await openRegister(method);

    expect(screen.getByText(notice)).toBeTruthy();
    expect(field(label).disabled).toBe(true);
    expect(bankAccountService.list).not.toHaveBeenCalled();
    expect(cashRegisterLookupFacade.getCashRegisters).not.toHaveBeenCalled();
  });

  it("un 403 del servidor se explica igual y un fallo distinto se informa como error", async () => {
    granted.add(CUSTOMERS).add(BANKS).add(REGISTERS);
    vi.mocked(bankAccountService.list).mockRejectedValue(
      Object.assign(new Error("403"), { isAxiosError: true, response: { status: 403 } }),
    );
    vi.mocked(cashRegisterLookupFacade.getCashRegisters).mockRejectedValue(new Error("red"));
    await openRegister("pm-transfer");

    expect(await screen.findByText(/No tiene permiso para consultar cuentas/)).toBeTruthy();
    set(/^Medio de pago/, "pm-cash");
    expect(await screen.findByText("No se pudieron cargar las cajas.")).toBeTruthy();
  });

  it("sin permiso de clientes explica la causa en lugar del buscador", async () => {
    await openRegister("pm-cash");

    expect(screen.getByText(/No tiene permiso para buscar clientes/)).toBeTruthy();
    expect(screen.queryByText("elegir cliente")).toBeNull();
  });
});

describe("Aplicación — CxC filtradas y paginadas en el servidor", () => {
  const render_ = () =>
    wrap(
      <ApplyCustomerAdvanceModal open advance={ADVANCE} onClose={vi.fn()} onApplied={vi.fn()} />,
    );

  it("pide una sola página del cliente y las siguientes solo bajo demanda", async () => {
    granted.add(SALES);
    vi.mocked(receivableService.list)
      .mockResolvedValueOnce(page(1, 50, 120))
      .mockResolvedValueOnce(page(51, 50, 120));
    render_();
    await screen.findByRole("option", { name: /F-50 / });

    expect(receivableService.list).toHaveBeenCalledTimes(1);
    expect(receivableService.list).toHaveBeenCalledWith("pending", 1, 50, "cust-1");

    fireEvent.click(
      screen.getByRole("button", { name: "Cargar más cuentas por cobrar (50 de 120)" }),
    );
    await screen.findByRole("option", { name: /F-100 / });
    expect(receivableService.list).toHaveBeenCalledTimes(2);
    expect(receivableService.list).toHaveBeenLastCalledWith("pending", 2, 50, "cust-1");
    expect(screen.getByRole("option", { name: /F-1 / })).toBeTruthy();
    expect(screen.getByText("Cargar más cuentas por cobrar (100 de 120)")).toBeTruthy();
  });

  it("distingue un cliente sin deuda de la falta de permiso", async () => {
    granted.add(SALES);
    vi.mocked(receivableService.list).mockResolvedValue(page(1, 0, 0));
    render_();
    expect(
      await screen.findByText("El cliente no tiene cuentas por cobrar pendientes."),
    ).toBeTruthy();
    expect(screen.queryByText(/No tiene permiso/)).toBeNull();
    cleanup();
    vi.clearAllMocks();
    granted.clear();
    resetCatalogs();

    render_();
    expect(screen.getByText(/No tiene permiso para consultar cuentas por cobrar/)).toBeTruthy();
    expect(screen.queryByText("El cliente no tiene cuentas por cobrar pendientes.")).toBeNull();
    expect(receivableService.list).not.toHaveBeenCalled();
  });
});
