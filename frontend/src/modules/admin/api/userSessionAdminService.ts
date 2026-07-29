import { api } from "../../lib/api";
import type { ApiResponse, PagedResponse } from "../../../types/api";

/**
 * Fase 11 — consume exclusivamente los endpoints ya existentes de Fase 10
 * (api/v1/admin/access/sessions*). No expone RefreshTokenId ni ningún dato interno:
 * el DTO refleja exactamente lo que el backend ya decidió exponer.
 */
export type UserSessionAdminDto = {
  sessionId: string;
  identityUserId: string;
  companyId: string;
  branchId: string;
  terminalId: string;
  status: "Active" | "ClosedManually" | "ClosedByNewLogin" | "Expired";
  createdAt: string;
  updatedAt: string | null;
};

export type SessionStatisticsDto = {
  active: number;
  closedManually: number;
  closedByNewLogin: number;
  expired: number;
  total: number;
};

export type GetUserSessionsPagedParams = {
  identityUserId?: string;
  companyId?: string;
  status?: string;
  fromUtc?: string;
  toUtc?: string;
  pageNumber?: number;
  pageSize?: number;
};

function buildQuery(
  params: Record<string, string | number | undefined>,
): string {
  const q = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") q.set(key, String(value));
  }
  const qs = q.toString();
  return qs ? `?${qs}` : "";
}

export const userSessionAdminService = {
  getPaged: (params: GetUserSessionsPagedParams = {}) =>
    api
      .get<ApiResponse<PagedResponse<UserSessionAdminDto>>>(
        `/api/v1/admin/access/sessions${buildQuery(params)}`,
      )
      .then((r) => r.data.data),

  getStatistics: (companyId?: string) =>
    api
      .get<ApiResponse<SessionStatisticsDto>>(
        `/api/v1/admin/access/sessions/statistics${buildQuery({ companyId })}`,
      )
      .then((r) => r.data.data),

  close: (sessionId: string) =>
    api
      .post<ApiResponse<string>>(
        `/api/v1/admin/access/sessions/${sessionId}/close`,
      )
      .then((r) => r.data.data),
};
