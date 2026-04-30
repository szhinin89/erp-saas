export interface ApiResponse<T> {
  success: boolean;
  message: string;
  responseObject: T;
}

export interface PagedResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

