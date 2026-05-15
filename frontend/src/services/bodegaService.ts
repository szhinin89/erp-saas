import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type CatalogActiveStatus = 'all' | 'active' | 'inactive';

export type BodegaDto = {
  id: string;
  branchId: string;
  name: string;
  address: string | null;
  manager: string | null;
  isActive: boolean;
};

export type BodegaDetailDto = BodegaDto & {
  createdAt: string;
  updatedAt: string | null;
  createdBy: string;
  updatedBy: string | null;
};

export type BodegaPayload = {
  branchId: string;
  name: string;
  address?: string | null;
  manager?: string | null;
};

function get<T>(url: string) {
  return api.get<ApiResponse<T>>(url).then((r) => r.data.responseObject);
}

function post<T>(url: string, body: unknown) {
  return api.post<ApiResponse<T>>(url, body).then((r) => r.data.responseObject);
}

function put<T>(url: string, body: unknown) {
  return api.put<ApiResponse<T>>(url, body).then((r) => r.data.responseObject);
}

function patch<T>(url: string) {
  return api.patch<ApiResponse<T>>(url, {}).then((r) => r.data.responseObject);
}

function listQuery(activeStatus: CatalogActiveStatus, search?: string, sucursalId?: string) {
  const q = new URLSearchParams();
  q.set('activeStatus', activeStatus);
  if (search?.trim()) q.set('search', search.trim());
  if (sucursalId?.trim()) q.set('sucursalId', sucursalId.trim());
  return `?${q.toString()}`;
}

export const bodegaService = {
  list: (activeStatus: CatalogActiveStatus = 'all', search?: string, sucursalId?: string) =>
    get<BodegaDto[]>(`/api/bodegas${listQuery(activeStatus, search, sucursalId)}`),

  getById: (id: string) => get<BodegaDetailDto>(`/api/bodegas/${id}`),

  create: (body: BodegaPayload) =>
    post<BodegaDto>('/api/bodegas', body),

  update: (id: string, body: BodegaPayload & { id: string }) =>
    put<BodegaDto>(`/api/bodegas/${id}`, body),

  disable: (id: string) => patch<BodegaDto>(`/api/bodegas/${id}/disable`),
  enable: (id: string) => patch<BodegaDto>(`/api/bodegas/${id}/enable`),
};

