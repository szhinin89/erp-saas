import { apiGet, apiPost } from "../../lib/apiEnvelope";
import { api } from "../../lib/api";
import type {
  ImportBatchConfirmResultDto,
  ImportBatchDto,
  ImportBatchRowPreviewDto,
  ImportSeverity,
  ImportType,
  PagedResult,
} from "../types/importBatch.types";

const BASE = "/api/v1/initial-load";

export const initialLoadService = {
  createBatch(importType: ImportType, label?: string): Promise<ImportBatchDto> {
    return apiPost<ImportBatchDto>(`${BASE}/batches`, { importType, label });
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
};

export type { ImportSeverity };
