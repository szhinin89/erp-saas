import { apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";
import { api } from "../../lib/api";
import type {
  ImportBatchConfirmResultDto,
  ImportBatchDto,
  ImportBatchRowPreviewDto,
  ImportSeverity,
  ImportType,
  InitialLoadClosureDto,
  OpeningBalanceDateDto,
  OpeningBalancePostingDto,
  OpeningBalanceReconciliationDto,
  OpeningJournalEntryLineInput,
  OpeningJournalEntryPostingDto,
  OpeningJournalEntryReversalDto,
  PagedResult,
} from "../types/importBatch.types";

const BASE = "/api/v1/initial-load";

export const initialLoadService = {
  createBatch(
    importType: ImportType,
    label?: string,
    autoCreateCatalogValues?: boolean,
  ): Promise<ImportBatchDto> {
    return apiPost<ImportBatchDto>(`${BASE}/batches`, {
      importType,
      label,
      autoCreateCatalogValues,
    });
  },

  uploadFile(
    batchId: string,
    file: File,
    onProgress?: (percent: number) => void,
  ): Promise<ImportBatchDto> {
    const formData = new FormData();
    formData.append("file", file);
    return apiPost<ImportBatchDto>(`${BASE}/batches/${batchId}/upload`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
      onUploadProgress: (event) => {
        if (!onProgress || !event.total) return;
        onProgress(Math.round((event.loaded / event.total) * 100));
      },
    });
  },

  validateBatch(batchId: string): Promise<ImportBatchDto> {
    return apiPost<ImportBatchDto>(`${BASE}/batches/${batchId}/validate`, {});
  },

  getStatus(batchId: string): Promise<ImportBatchDto> {
    return apiGet<ImportBatchDto>(`${BASE}/batches/${batchId}`);
  },

  preview(
    batchId: string,
    page: number,
    pageSize: number,
    onlyWithBlockingIssue?: boolean,
  ): Promise<PagedResult<ImportBatchRowPreviewDto>> {
    return apiGet<PagedResult<ImportBatchRowPreviewDto>>(
      `${BASE}/batches/${batchId}/preview`,
      {
        params: { page, pageSize, onlyWithBlockingIssue },
      },
    );
  },

  confirmBatch(batchId: string): Promise<ImportBatchConfirmResultDto> {
    return apiPost<ImportBatchConfirmResultDto>(`${BASE}/batches/${batchId}/confirm`, {});
  },

  cancelBatch(batchId: string): Promise<boolean> {
    return apiPost<boolean>(`${BASE}/batches/${batchId}/cancel`, {});
  },

  getHistory(
    importType: ImportType | undefined,
    page: number,
    pageSize: number,
  ): Promise<PagedResult<ImportBatchDto>> {
    return apiGet<PagedResult<ImportBatchDto>>(`${BASE}/batches`, {
      params: { importType, page, pageSize },
    });
  },

  async downloadTemplate(importType: ImportType): Promise<Blob> {
    const { data } = await api.get<Blob>(`${BASE}/templates/${importType}`, {
      responseType: "blob",
    });
    return data;
  },

  getOpeningBalanceDate(): Promise<OpeningBalanceDateDto> {
    return apiGet<OpeningBalanceDateDto>(`${BASE}/opening-balance-date`);
  },

  setOpeningBalanceDate(openingBalanceDate: string): Promise<OpeningBalanceDateDto> {
    return apiPut<OpeningBalanceDateDto>(`${BASE}/opening-balance-date`, { openingBalanceDate });
  },

  /** IL-7C — conciliación de apertura (solo lectura). */
  getOpeningReconciliation(): Promise<OpeningBalanceReconciliationDto> {
    return apiGet<OpeningBalanceReconciliationDto>(`${BASE}/opening-reconciliation`);
  },

  /** IL-8E — cierre DEFINITIVO e irreversible de la carga inicial (solo sin blockers). */
  closeInitialLoad(): Promise<InitialLoadClosureDto> {
    return apiPost<InitialLoadClosureDto>(`${BASE}/close`, {});
  },

  /** IL-7B — contabiliza o reintenta el asiento de apertura del lote. */
  postOpeningBalance(batchId: string): Promise<OpeningBalancePostingDto> {
    return apiPost<OpeningBalancePostingDto>(`${BASE}/batches/${batchId}/opening-posting`, {});
  },

  /** IL-8A — publica (o reintenta) la versión vigente del ASI de apertura. */
  publishOpeningJournal(lines: OpeningJournalEntryLineInput[]): Promise<OpeningJournalEntryPostingDto> {
    return apiPost<OpeningJournalEntryPostingDto>(`${BASE}/opening-journal-entry`, { lines });
  },

  /** IL-8B — "Corregir apertura" paso 1: reversa la versión vigente publicada (motivo obligatorio). */
  reverseOpeningJournal(postingId: string, reason: string): Promise<OpeningJournalEntryReversalDto> {
    return apiPost<OpeningJournalEntryReversalDto>(
      `${BASE}/opening-journal-entry/${postingId}/reverse`,
      { reason },
    );
  },
};

export type { ImportSeverity };
