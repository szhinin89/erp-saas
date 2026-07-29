export type ApiSeverity = "success" | "error" | "warning" | "info";

export interface ApiResponseMessage {
  user: string;
  dev: string | null;
}

export interface ApiResponseMeta {
  correlationId: string;
  timestamp: string;
  traceId?: string | null;
}

export interface ApiResponse<T> {
  code: string;
  severity: ApiSeverity;
  message: ApiResponseMessage;
  data: T;
  meta: ApiResponseMeta;
}

export interface PagedResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}
