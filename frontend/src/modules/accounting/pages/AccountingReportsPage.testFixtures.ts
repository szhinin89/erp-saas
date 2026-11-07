import type {
  AccountDto,
  AccountingCompleteness,
  GeneralJournalLineDto,
  GeneralLedgerAccountDto,
  GetBalanceSheetReportResponse,
  GetGeneralJournalReportResponse,
  GetIncomeStatementReportResponse,
  GetTrialBalanceReportResponse,
} from "../api/accountingApi";

/**
 * ACCOUNTING-REPORTS-PRO-15 — datos de ejemplo de las pruebas de `/accounting/reports`. Todos son
 * COHERENTES: las líneas suman los totales, los subtotales los resultados y los saldos acumulados
 * parten del saldo inicial (incluidos los escenarios de descuadre o pérdida, cuyas cifras también
 * cuadran entre sí). Ningún importe se recalcula en el frontend: se comprueba que se muestran.
 */

export const COMPLETE: AccountingCompleteness = { isComplete: true, asOf: "2026-11-06", reasons: [] };
export const INCOMPLETE: AccountingCompleteness = {
  isComplete: false,
  asOf: "2026-11-06",
  reasons: [{ code: "PendingPosting", count: 2 }],
};

export function journalLine(over: Partial<GeneralJournalLineDto>): GeneralJournalLineDto {
  return {
    journalEntryId: "je-1",
    entryNumber: 12,
    entryDate: "2026-11-02",
    description: "Venta de contado",
    sourceModule: "Sales",
    sourceEventType: "InvoiceIssued",
    sourceEventId: "inv-1",
    sourceDocumentType: "Factura",
    sourceDocumentNumber: "001-001-000000123",
    accountId: "acc-cash",
    accountCode: "1.1.01",
    accountName: "Caja",
    debit: 0,
    credit: 0,
    ...over,
  };
}

// Asiento 12: dos líneas idénticas de Caja (50 + 50, antes generaban la misma key) contra Ventas 100.
// Asiento 13: compra sin número de documento, Inventario 30 contra Proveedores 30. Página: 130 = 130.
const PURCHASE = {
  journalEntryId: "je-2",
  entryNumber: 13,
  description: "Compra de mercadería",
  sourceDocumentNumber: null,
  sourceModule: "Purchases",
  sourceEventType: "InvoiceReceived",
};
export const JOURNAL: GetGeneralJournalReportResponse = {
  lines: [
    journalLine({ debit: 50 }),
    journalLine({ debit: 50 }),
    journalLine({ accountId: "acc-sales", accountCode: "4.1.01", accountName: "Ventas", credit: 100 }),
    journalLine({ ...PURCHASE, accountId: "acc-inv", accountCode: "1.1.03", accountName: "Inventario", debit: 30 }),
    journalLine({ ...PURCHASE, accountId: "acc-sup", accountCode: "2.1.01", accountName: "Proveedores", credit: 30 }),
  ],
  totalDebit: 130,
  totalCredit: 130,
  pageNumber: 1,
  pageSize: 50,
  totalCount: 120,
  completeness: COMPLETE,
};

// Página cuyas líneas no cuadran (asiento de un solo lado): sus totales coinciden con sus líneas.
export const JOURNAL_UNBALANCED_PAGE: GetGeneralJournalReportResponse = {
  ...JOURNAL,
  lines: [journalLine({ ...PURCHASE, accountId: "acc-inv", accountCode: "1.1.03", accountName: "Inventario", debit: 30 })],
  totalDebit: 30,
  totalCredit: 0,
  totalCount: 1,
};

export function ledgerAccount(over: Partial<GeneralLedgerAccountDto>): GeneralLedgerAccountDto {
  return {
    accountId: "acc",
    accountCode: "1",
    accountName: "Cuenta",
    accountType: "Asset",
    nature: "Debit",
    openingBalance: 0,
    periodDebit: 0,
    periodCredit: 0,
    closingBalance: 0,
    movements: [],
    ...over,
  };
}

const deposit = { journalEntryId: "je-9", entryNumber: 9, entryDate: "2026-11-03", description: "Depósito",
  sourceModule: "Finance", sourceDocumentType: null, sourceDocumentNumber: "DEP-1" };
// Bancos: saldo inicial 40 + 80 − 20 = 100 (saldo acumulado 120 → 100 = saldo final).
export const LEDGER_ACCOUNTS: GeneralLedgerAccountDto[] = [
  ledgerAccount({ accountId: "g-1", accountCode: "1", accountName: "Activo" }), // agrupadora sin saldo
  ledgerAccount({ accountId: "a-cash", accountCode: "1.1.01", accountName: "Caja", openingBalance: 250, closingBalance: 250 }),
  ledgerAccount({
    accountId: "a-bank",
    accountCode: "1.1.02",
    accountName: "Bancos",
    openingBalance: 40,
    periodDebit: 80,
    periodCredit: 20,
    closingBalance: 100,
    movements: [
      { ...deposit, debit: 80, credit: 0, runningBalance: 120 },
      { ...deposit, debit: 0, credit: 20, runningBalance: 100 },
    ],
  }),
  ledgerAccount({ accountId: "a-idle", accountCode: "1.1.09", accountName: "Caja chica" }), // imputable sin saldo
];

export function chartAccount(
  id: string,
  code: string,
  name: string,
  allowsPosting: boolean,
  parentAccountId: string | null = null,
): AccountDto {
  return {
    id, code, name, parentAccountId, parentAccountCode: null, parentAccountName: null, level: code.split(".").length,
    accountType: "Asset", nature: "Debit", allowsPosting, isActive: true, createdAt: "2026-01-01T00:00:00Z", updatedAt: null,
  };
}

export const CHART: AccountDto[] = [
  chartAccount("g-1", "1", "Activo", false),
  chartAccount("a-cash", "1.1.01", "Caja", true, "g-1"),
  chartAccount("a-bank", "1.1.02", "Bancos", true, "g-1"),
  chartAccount("a-idle", "1.1.09", "Caja chica", true, "g-1"),
];

// Movimientos 75 = 75; saldo inicial 100 = 100; saldo final 155 = 155 (cada saldo final = inicial ± movimientos).
export const TRIAL: GetTrialBalanceReportResponse = {
  lines: [
    { accountId: "a-cash", accountCode: "1.1.01", accountName: "Caja", accountType: "Asset",
      openingDebit: 100, openingCredit: 0, periodDebit: 40, periodCredit: 10, closingDebit: 130, closingCredit: 0 },
    { accountId: "a-bank", accountCode: "1.1.02", accountName: "Bancos", accountType: "Asset",
      openingDebit: 0, openingCredit: 0, periodDebit: 25, periodCredit: 0, closingDebit: 25, closingCredit: 0 },
    { accountId: "l-sup", accountCode: "2.1.01", accountName: "Proveedores", accountType: "Liability",
      openingDebit: 0, openingCredit: 100, periodDebit: 10, periodCredit: 40, closingDebit: 0, closingCredit: 130 },
    { accountId: "i-sales", accountCode: "4.1.01", accountName: "Ventas", accountType: "Income",
      openingDebit: 0, openingCredit: 0, periodDebit: 0, periodCredit: 25, closingDebit: 0, closingCredit: 25 },
  ],
  totalOpeningDebit: 100,
  totalOpeningCredit: 100,
  totalPeriodDebit: 75,
  totalPeriodCredit: 75,
  totalClosingDebit: 155,
  totalClosingCredit: 155,
  isBalanced: true,
  completeness: COMPLETE,
};

// Ventas acreditadas por 20 (no 25): movimientos 75 ≠ 70 y saldo final 155 ≠ 150; saldo inicial sigue 100 = 100.
export const TRIAL_UNBALANCED: GetTrialBalanceReportResponse = {
  ...TRIAL,
  lines: TRIAL.lines.map((l) => (l.accountId === "i-sales" ? { ...l, periodCredit: 20, closingCredit: 20 } : l)),
  totalPeriodCredit: 70,
  totalClosingCredit: 150,
  isBalanced: false,
};

// 500 − 300 = 200 bruta; 200 − 0 = 200 neta.
export const INCOME: GetIncomeStatementReportResponse = {
  incomeLines: [{ accountId: "i-1", accountCode: "4.1.01", accountName: "Ventas", amount: 500 }],
  totalIncome: 500,
  costLines: [{ accountId: "c-1", accountCode: "5.1.01", accountName: "Costo de ventas", amount: 300 }],
  totalCost: 300,
  grossProfit: 200,
  expenseLines: [],
  totalExpense: 0,
  netProfit: 200,
  completeness: COMPLETE,
};

// 500 − 600 = −100 bruta; −100 − 50 = −150 neta.
export const INCOME_LOSS: GetIncomeStatementReportResponse = {
  ...INCOME,
  costLines: [{ accountId: "c-1", accountCode: "5.1.01", accountName: "Costo de ventas", amount: 600 }],
  totalCost: 600,
  grossProfit: -100,
  expenseLines: [{ accountId: "x-1", accountCode: "6.1.01", accountName: "Sueldos", amount: 50 }],
  totalExpense: 50,
  netProfit: -150,
};

// Activo 1000 (700 + 300) = Pasivo 400 + Patrimonio 600 (500 + 100 + 0).
export const BALANCE: GetBalanceSheetReportResponse = {
  assetLines: [
    { accountId: "a-1", accountCode: "1.1.01", accountName: "Caja", amount: 700 },
    { accountId: "a-2", accountCode: "1.2.01", accountName: "Equipos", amount: 300 },
  ],
  totalAssets: 1000,
  liabilityLines: [{ accountId: "l-1", accountCode: "2.1.01", accountName: "Proveedores", amount: 400 }],
  totalLiabilities: 400,
  equityLines: [{ accountId: "e-1", accountCode: "3.1.01", accountName: "Capital", amount: 500 }],
  currentYearResult: 100,
  priorYearsUnclosedResult: 0,
  totalEquity: 600,
  difference: 0,
  isBalanced: true,
  completeness: COMPLETE,
};

// Caja 725: Activo 1025 − (400 + 600) = diferencia 25.
export const BALANCE_UNBALANCED: GetBalanceSheetReportResponse = {
  ...BALANCE,
  assetLines: [{ ...BALANCE.assetLines[0], amount: 725 }, BALANCE.assetLines[1]],
  totalAssets: 1025,
  difference: 25,
  isBalanced: false,
};

/** Plan de Cuentas del Balance General: raíces 1/2/3, grupos 1.1, 1.2 y 2.1 (3.1.01 cuelga de la raíz). */
export const BALANCE_CHART: AccountDto[] = [
  chartAccount("r-1", "1", "Activo", false),
  chartAccount("g-11", "1.1", "Activo corriente", false, "r-1"),
  chartAccount("g-12", "1.2", "Activo no corriente", false, "r-1"),
  chartAccount("a-1", "1.1.01", "Caja", true, "g-11"),
  chartAccount("a-2", "1.2.01", "Equipos", true, "g-12"),
  chartAccount("r-2", "2", "Pasivo", false),
  chartAccount("g-21", "2.1", "Pasivo corriente", false, "r-2"),
  chartAccount("l-1", "2.1.01", "Proveedores", true, "g-21"),
  chartAccount("r-3", "3", "Patrimonio", false),
  chartAccount("e-1", "3.1.01", "Capital", true, "r-3"),
];
