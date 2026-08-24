export type ImportType =
  | "Customers"
  | "Suppliers"
  | "Items"
  | "Prices"
  | "InitialStock";

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
