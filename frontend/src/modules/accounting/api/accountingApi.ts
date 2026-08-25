import { apiGet, apiPatch, apiPost } from "../../lib/apiEnvelope";

const BASE = "/api/v1/accounting";

// ── DTOs (espejo exacto de AccountDto en ERP.Application) ──

export interface AccountDto {
  id: string;
  code: string;
  name: string;
  parentAccountId: string | null;
  parentAccountCode: string | null;
  parentAccountName: string | null;
  level: number;
  accountType: string;
  nature: string;
  allowsPosting: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateAccountPayload {
  code: string;
  name: string;
  parentAccountId: string | null;
  accountType: string;
  nature: string;
  allowsPosting: boolean;
}

export interface UpdateAccountPayload {
  id: string;
  name: string;
  parentAccountId: string | null;
  allowsPosting: boolean;
}

// ── DTOs (espejo exacto de JournalEntryListItemDto/JournalEntryDetailDto/JournalEntryLineDto en ERP.Application) ──

export interface JournalEntryListItemDto {
  id: string;
  entryNumber: number | null;
  entryDate: string;
  sourceModule: string;
  sourceEventType: string;
  sourceEventId: string;
  description: string;
  totalDebit: number;
  totalCredit: number;
  status: string;
  createdAt: string;
  sourceDocumentType: string | null;
  sourceDocumentNumber: string | null;
  sourceDocumentDate: string | null;
  sourcePartyName: string | null;
  sourceStatus: string | null;
  sourceRoute: string | null;
}

export interface JournalEntryLineDto {
  id: string;
  accountId: string;
  accountCode: string;
  accountName: string;
  description: string | null;
  debit: number;
  credit: number;
  sortOrder: number;
}

export interface JournalEntryDetailDto {
  id: string;
  entryNumber: number | null;
  entryDate: string;
  accountingPeriodId: string;
  fiscalYear: number;
  sourceModule: string;
  sourceEventType: string;
  sourceEventId: string;
  description: string;
  status: string;
  postedAtUtc: string | null;
  originalJournalEntryId: string | null;
  originalJournalEntryNumber: number | null;
  originalJournalEntryDate: string | null;
  reverseJournalEntryId: string | null;
  reverseJournalEntryNumber: number | null;
  reverseJournalEntryDate: string | null;
  reversedAtUtc: string | null;
  reverseReason: string | null;
  lines: JournalEntryLineDto[];
  totalDebit: number;
  totalCredit: number;
  isBalanced: boolean;
  createdAt: string;
  sourceDocumentType: string | null;
  sourceDocumentNumber: string | null;
  sourceDocumentDate: string | null;
  sourcePartyName: string | null;
  sourceStatus: string | null;
  sourceRoute: string | null;
}

export interface GetJournalEntriesResponse {
  items: JournalEntryListItemDto[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface JournalEntriesFilter {
  status?: string;
  fromDate?: string;
  toDate?: string;
  sourceModule?: string;
}

// ── DTOs (espejo exacto de GeneralJournal/GeneralLedger/TrialBalance en ERP.Application — ACCOUNTING-REPORTS-09) ──

export interface GeneralJournalLineDto {
  journalEntryId: string;
  entryNumber: number | null;
  entryDate: string;
  description: string;
  sourceModule: string;
  sourceEventType: string;
  sourceEventId: string;
  sourceDocumentType: string | null;
  sourceDocumentNumber: string | null;
  accountId: string;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
}

export interface GetGeneralJournalReportResponse {
  lines: GeneralJournalLineDto[];
  totalDebit: number;
  totalCredit: number;
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface GeneralLedgerMovementDto {
  journalEntryId: string;
  entryNumber: number | null;
  entryDate: string;
  description: string;
  sourceModule: string;
  sourceDocumentType: string | null;
  sourceDocumentNumber: string | null;
  debit: number;
  credit: number;
  runningBalance: number;
}

export interface GeneralLedgerAccountDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  accountType: string;
  nature: string;
  openingBalance: number;
  periodDebit: number;
  periodCredit: number;
  closingBalance: number;
  movements: GeneralLedgerMovementDto[];
}

export interface GetGeneralLedgerReportResponse {
  accounts: GeneralLedgerAccountDto[];
}

export interface TrialBalanceLineDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  accountType: string;
  openingDebit: number;
  openingCredit: number;
  periodDebit: number;
  periodCredit: number;
  closingDebit: number;
  closingCredit: number;
}

export interface GetTrialBalanceReportResponse {
  lines: TrialBalanceLineDto[];
  totalOpeningDebit: number;
  totalOpeningCredit: number;
  totalPeriodDebit: number;
  totalPeriodCredit: number;
  totalClosingDebit: number;
  totalClosingCredit: number;
  isBalanced: boolean;
}

// ── DTOs (espejo exacto de FinancialStatementLineDto/GetIncomeStatementReportResponse/GetBalanceSheetReportResponse — ACCOUNTING-FINANCIAL-STATEMENTS-10) ──

export interface FinancialStatementLineDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  amount: number;
}

export interface GetIncomeStatementReportResponse {
  incomeLines: FinancialStatementLineDto[];
  totalIncome: number;
  costLines: FinancialStatementLineDto[];
  totalCost: number;
  grossProfit: number;
  expenseLines: FinancialStatementLineDto[];
  totalExpense: number;
  netProfit: number;
}

export interface GetBalanceSheetReportResponse {
  assetLines: FinancialStatementLineDto[];
  totalAssets: number;
  liabilityLines: FinancialStatementLineDto[];
  totalLiabilities: number;
  equityLines: FinancialStatementLineDto[];
  totalEquity: number;
  difference: number;
  isBalanced: boolean;
}

/**
 * Consume `AccountingController`. `journal-entries*` es solo lectura (ACCOUNTING-LEDGER-
 * VISIBILITY-01) — el motor de contabilización sigue siendo responsabilidad exclusiva del
 * backend (Posting Engine). `accounts*` sí admite Create/Update/Enable/Disable
 * (ACCOUNTING-CHART-OF-ACCOUNTS-02) — administración del Plan de Cuentas, nunca del motor.
 * `reports/*` (ACCOUNTING-REPORTS-09 / ACCOUNTING-FINANCIAL-STATEMENTS-10) es solo lectura —
 * nunca recalcula, solo lee JournalEntry/JournalEntryLine ya contabilizados; desde
 * ACCOUNTING-FINANCIAL-STATEMENTS-10 el backend expone esas rutas vía
 * `AccountingReportsController` (split mecánico, misma ruta `api/v1/accounting/reports/*`).
 */
export const accountingApi = {
  listAccounts: () => apiGet<AccountDto[]>(`${BASE}/accounts`),

  getAccountById: (id: string) => apiGet<AccountDto>(`${BASE}/accounts/${id}`),

  getAccountByCode: (code: string) =>
    apiGet<AccountDto>(`${BASE}/accounts/by-code/${encodeURIComponent(code)}`),

  createAccount: (payload: CreateAccountPayload) =>
    apiPost<AccountDto>(`${BASE}/accounts`, payload),

  updateAccount: (id: string, payload: UpdateAccountPayload) =>
    apiPatch<AccountDto>(`${BASE}/accounts/${id}`, payload),

  enableAccount: (id: string) => apiPatch<AccountDto>(`${BASE}/accounts/${id}/enable`, {}),

  disableAccount: (id: string) => apiPatch<AccountDto>(`${BASE}/accounts/${id}/disable`, {}),

  listJournalEntries: (page = 1, pageSize = 20, filter: JournalEntriesFilter = {}) => {
    const params = new URLSearchParams();
    params.set("pageNumber", String(page));
    params.set("pageSize", String(pageSize));
    if (filter.status) params.set("status", filter.status);
    if (filter.fromDate) params.set("fromDate", filter.fromDate);
    if (filter.toDate) params.set("toDate", filter.toDate);
    if (filter.sourceModule) params.set("sourceModule", filter.sourceModule);
    return apiGet<GetJournalEntriesResponse>(`${BASE}/journal-entries?${params}`);
  },

  getJournalEntryById: (id: string) =>
    apiGet<JournalEntryDetailDto>(`${BASE}/journal-entries/${id}`),

  getJournalEntriesBySource: (sourceModule: string, sourceDocumentId: string) =>
    apiGet<JournalEntryListItemDto[]>(
      `${BASE}/journal-entries/by-source/${encodeURIComponent(sourceModule)}/${sourceDocumentId}`,
    ),

  getGeneralJournalReport: (params: {
    fromDate: string;
    toDate: string;
    sourceModule?: string;
    search?: string;
    pageNumber?: number;
    pageSize?: number;
  }) => {
    const qs = new URLSearchParams();
    qs.set("fromDate", params.fromDate);
    qs.set("toDate", params.toDate);
    if (params.sourceModule) qs.set("sourceModule", params.sourceModule);
    if (params.search) qs.set("search", params.search);
    qs.set("pageNumber", String(params.pageNumber ?? 1));
    qs.set("pageSize", String(params.pageSize ?? 50));
    return apiGet<GetGeneralJournalReportResponse>(`${BASE}/reports/general-journal?${qs}`);
  },

  getGeneralLedgerReport: (params: {
    fromDate: string;
    toDate: string;
    accountId?: string;
    accountCodeFrom?: string;
    accountCodeTo?: string;
  }) => {
    const qs = new URLSearchParams();
    qs.set("fromDate", params.fromDate);
    qs.set("toDate", params.toDate);
    if (params.accountId) qs.set("accountId", params.accountId);
    if (params.accountCodeFrom) qs.set("accountCodeFrom", params.accountCodeFrom);
    if (params.accountCodeTo) qs.set("accountCodeTo", params.accountCodeTo);
    return apiGet<GetGeneralLedgerReportResponse>(`${BASE}/reports/general-ledger?${qs}`);
  },

  getTrialBalanceReport: (params: {
    fromDate: string;
    toDate: string;
    includeZeroMovementAccounts?: boolean;
  }) => {
    const qs = new URLSearchParams();
    qs.set("fromDate", params.fromDate);
    qs.set("toDate", params.toDate);
    if (params.includeZeroMovementAccounts) qs.set("includeZeroMovementAccounts", "true");
    return apiGet<GetTrialBalanceReportResponse>(`${BASE}/reports/trial-balance?${qs}`);
  },

  getIncomeStatementReport: (params: { fromDate: string; toDate: string }) => {
    const qs = new URLSearchParams();
    qs.set("fromDate", params.fromDate);
    qs.set("toDate", params.toDate);
    return apiGet<GetIncomeStatementReportResponse>(`${BASE}/reports/income-statement?${qs}`);
  },

  getBalanceSheetReport: (params: { asOfDate: string }) => {
    const qs = new URLSearchParams();
    qs.set("asOfDate", params.asOfDate);
    return apiGet<GetBalanceSheetReportResponse>(`${BASE}/reports/balance-sheet?${qs}`);
  },
};
