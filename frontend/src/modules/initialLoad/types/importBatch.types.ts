export type ImportType =
  | "Customers"
  | "Suppliers"
  | "Items"
  | "Prices"
  | "InitialStock"
  | "InitialReceivables"
  | "InitialPayables";

export type ImportStatus =
  | "Draft"
  | "Uploaded"
  | "Validating"
  | "Validated"
  | "Confirming"
  | "Completed"
  | "PartiallyCompleted"
  | "Failed"
  | "Cancelled";

export type ImportSeverity = "Error" | "Warning";

export interface ImportBatchDto {
  id: string;
  importType: ImportType;
  status: ImportStatus;
  label: string | null;
  autoCreateCatalogValues: boolean;
  totalRows: number;
  validRows: number;
  issueRows: number;
  warningRows: number;
  importedRows: number;
  validatedAt: string | null;
  confirmedAt: string | null;
  cancelledAt: string | null;
  failureReason: string | null;
  createdAt: string;
}

export interface ImportBatchIssueDto {
  id: string;
  rowNumber: number;
  fieldName: string | null;
  severity: ImportSeverity;
  code: string;
  message: string;
}

export interface ImportBatchRowPreviewDto {
  id: string;
  rowNumber: number;
  hasBlockingIssue: boolean;
  isImported: boolean;
  createdBusinessPartnerId: string | null;
  rawData: Record<string, string | null>;
  issues: ImportBatchIssueDto[];
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface ImportBatchConfirmResultDto {
  importBatchId: string;
  status: ImportStatus;
  importedRows: number;
  failedRows: number;
}

/** IL-5A — Company.OpeningBalanceDate (Configuración → Implementación). Fechas "YYYY-MM-DD". */
export interface OpeningBalanceDateDto {
  openingBalanceDate: string | null;
  hasRealOperations: boolean;
  confirmedOpeningDates: string[];
  isLocked: boolean;
  lockReason: string | null;
}

/** IL-7C — conciliación de apertura contable vs saldos operativos confirmados. */
export type OpeningReconciliationStatus =
  | "Reconciled"
  | "Difference"
  | "PendingPosting"
  /** IL-8C — lotes conciliados, sin versión vigente del ASI de apertura publicada. */
  | "OpeningJournalPending";

/** IL-8C — estado del ASI de apertura, derivado solo de OpeningJournalEntryPosting (backend). */
export type OpeningJournalEntryState =
  | "Missing"
  | "Pending"
  | "Failed"
  | "Posted"
  | "ReversedNotReplaced";

export type OpeningBalancePostingStatus = "Pending" | "Posted" | "Failed";

export interface OpeningReconciliationBatchDto {
  importBatchId: string;
  importType: ImportType;
  factType: string;
  label: string | null;
  batchStatus: ImportStatus;
  confirmedAt: string | null;
  operationalAmount: number;
  accountingAmount: number;
  difference: number;
  status: OpeningReconciliationStatus;
  postingStatus: OpeningBalancePostingStatus | null;
  journalEntryId: string | null;
  journalEntryNumber: number | null;
  errorCode: string | null;
  errorMessage: string | null;
  canPost: boolean;
}

export interface OpeningReconciliationTypeDto {
  importType: ImportType;
  factType: string;
  accountId: string | null;
  accountCode: string | null;
  accountName: string | null;
  batchCount: number;
  operationalAmount: number;
  ledgerBalance: number | null;
  difference: number | null;
  status: OpeningReconciliationStatus;
}

export interface OpeningBridgeAccountDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  balanceAtCutoff: number | null;
  currentBalance: number;
  fromOpeningPostings: number;
  pendingReclassification: number;
}

export interface OpeningReconciliationBlockerDto {
  code: string;
  message: string;
  importBatchId: string | null;
}

export interface OpeningBalanceReconciliationDto {
  cutoffDate: string | null;
  status: OpeningReconciliationStatus;
  batches: OpeningReconciliationBatchDto[];
  types: OpeningReconciliationTypeDto[];
  bridgeAccount: OpeningBridgeAccountDto | null;
  openingJournalEntry: OpeningReconciliationJournalEntryDto;
  canCloseImplementation: boolean;
  blockers: OpeningReconciliationBlockerDto[];
  /** IL-8E — carga inicial cerrada definitivamente (irreversible). */
  isClosed: boolean;
  closedAt: string | null;
  closedBy: string | null;
  closedByName: string | null;
}

/** IL-8E — resultado del cierre definitivo de la carga inicial. */
export interface InitialLoadClosureDto {
  closedAt: string;
  closedBy: string | null;
  alreadyClosed: boolean;
}

/**
 * IL-8C — ASI de apertura dentro de la conciliación: datos de la versión VIGENTE (null si no hay)
 * más historial básico (`versionCount`, última versión reversada).
 */
export interface OpeningReconciliationJournalEntryDto {
  state: OpeningJournalEntryState;
  postingId: string | null;
  version: number | null;
  postingStatus: OpeningBalancePostingStatus | null;
  entryDate: string | null;
  totalAmount: number | null;
  lineCount: number | null;
  journalEntryId: string | null;
  journalEntryNumber: number | null;
  postedAt: string | null;
  attempts: number;
  errorCode: string | null;
  errorMessage: string | null;
  versionCount: number;
  lastSupersededVersion: number | null;
  lastSupersededAt: string | null;
}

/** IL-8A — línea Debe/Haber del ASI de apertura (exactamente un monto &gt; 0). */
export interface OpeningJournalEntryLineInput {
  accountId: string;
  debit: number;
  credit: number;
  description?: string | null;
}

/** IL-8A — resultado de publicar el ASI de apertura. */
export interface OpeningJournalEntryPostingDto {
  id: string;
  version: number;
  entryDate: string;
  totalAmount: number;
  lineCount: number;
  status: OpeningBalancePostingStatus;
  journalEntryId: string | null;
  postedAt: string | null;
  attempts: number;
  alreadyPosted: boolean;
}

/** IL-8B — resultado de reversar (corregir) el ASI de apertura vigente. */
export interface OpeningJournalEntryReversalDto {
  postingId: string;
  version: number;
  journalEntryId: string;
  reversalJournalEntryId: string;
  reversedAtUtc: string | null;
  reason: string | null;
  alreadyReversed: boolean;
}

/** IL-7B — resultado de contabilizar/reintentar la apertura de un lote. */
export interface OpeningBalancePostingDto {
  importBatchId: string;
  importType: ImportType;
  factType: string;
  entryDate: string;
  amount: number;
  status: OpeningBalancePostingStatus;
  journalEntryId: string | null;
  postedAt: string | null;
  attempts: number;
  alreadyPosted: boolean;
}
