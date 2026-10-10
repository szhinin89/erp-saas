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
export type OpeningReconciliationStatus = "Reconciled" | "Difference" | "PendingPosting";

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
  canCloseImplementation: boolean;
  blockers: OpeningReconciliationBlockerDto[];
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
