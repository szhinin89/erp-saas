import { vi } from "vitest";
import { addDaysIso, todayIso } from "../../../lib/formatters/dateFormatters";
import { bankAccountService } from "../api/bankAccountService";
import type { CustomerAdvanceDto } from "../api/customerAdvanceService";
import type { SalesReceivableDto } from "../api/receivableService";
import { cashRegisterLookupFacade } from "../../caja/facades/cashRegisterLookupFacade";
import { paymentMethodLookupFacade } from "../../sales/facades/paymentMethodLookupFacade";
import { sessionBranchFacade } from "../../session/facades/sessionBranchFacade";

/** Datos de prueba de Anticipos; los catálogos deben estar simulados por la prueba que los use. */
export const ADVANCE: CustomerAdvanceDto = {
  id: "adv-1",
  customerId: "cust-1",
  customerName: "Cliente Uno",
  branchId: "br-1",
  advanceDate: addDaysIso(todayIso(), -5),
  currencyCode: "USD",
  originalAmount: 100,
  availableAmount: 100,
  status: "Confirmed",
  paymentMethodId: "pm-transfer",
  companyBankAccountId: "bank-1",
  cashRegisterId: null,
  externalReference: "REF-1",
  note: "Reserva",
  movements: [],
  refundTransactions: [],
};

export function receivable(overrides: Partial<SalesReceivableDto> = {}): SalesReceivableDto {
  return {
    id: "ar-1",
    invoiceId: "inv-1",
    invoiceNumber: "F-1",
    customerId: "cust-1",
    customerName: "Cliente Uno",
    customerIdentification: "1710034065",
    branchId: "br-1",
    branchName: "Matriz",
    createdByUserId: null,
    createdByName: null,
    invoiceIssueDate: addDaysIso(todayIso(), -3),
    invoiceCreatedAt: "2026-09-01T10:00:00Z",
    dueDate: null,
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
    ...overrides,
  };
}

const method = (id: string, name: string, extra: Record<string, unknown>) => ({
  id,
  code: id,
  name,
  isActive: true,
  requiresReference: false,
  isCreditAllowed: false,
  sortOrder: 1,
  detailType: "None",
  affectsPhysicalCash: false,
  sriPaymentMethodCode: null,
  accountSource: "CashRegister",
  accountingAccountId: null,
  ...extra,
});

export function resetCatalogs() {
  vi.mocked(paymentMethodLookupFacade.list).mockResolvedValue([
    method("pm-cash", "Efectivo", { affectsPhysicalCash: true }),
    method("pm-transfer", "Transferencia", { detailType: "Transfer" }),
    method("pm-card", "Tarjeta", { detailType: "Card" }),
    method("pm-credit", "Crédito", { isCreditAllowed: true }),
  ] as never);
  vi.mocked(bankAccountService.list).mockResolvedValue([
    { id: "bank-1", displayName: "Banco Uno", isActive: true },
  ] as never);
  vi.mocked(cashRegisterLookupFacade.getCashRegisters).mockResolvedValue([
    { id: "reg-1", name: "Caja 1", isActive: true },
  ] as never);
  vi.mocked(sessionBranchFacade.listAvailable).mockResolvedValue({
    branches: [{ id: "br-1", name: "Matriz", isMainBranch: true }],
    loginMode: "AskBranch",
    defaultBranchId: null,
  });
}
