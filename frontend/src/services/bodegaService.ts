import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type CatalogActiveStatus = 'all' | 'active' | 'inactive';

export type WarehouseDto = {
  id: string;
  branchId: string;
  name: string;
  code: string | null;
  storageType: string | null;
  address: string | null;
  phone: string | null;
  email: string | null;
  manager: string | null;
  latitude: string | null;
  longitude: string | null;
  capacity: number | null;
  dailyDispatchGoal: number | null;
  isActive: boolean;
};

export type WarehouseDetailDto = WarehouseDto & {
  createdAt: string;
  updatedAt: string | null;
  createdBy: string;
  updatedBy: string | null;
};

export type WarehousePayload = {
  branchId: string;
  name: string;
  storageType?: string | null;
  address?: string | null;
  phone?: string | null;
  email?: string | null;
  manager?: string | null;
  latitude?: string | null;
  longitude?: string | null;
  capacity?: number | null;
  dailyDispatchGoal?: number | null;
};

/** @deprecated Use WarehouseDto */
export type BodegaDto = WarehouseDto;
/** @deprecated Use WarehouseDetailDto */
export type BodegaDetailDto = WarehouseDetailDto;
/** @deprecated Use WarehousePayload */
export type BodegaPayload = WarehousePayload;

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

function listQuery(activeStatus: CatalogActiveStatus, search?: string, branchId?: string) {
  const q = new URLSearchParams();
  q.set('activeStatus', activeStatus);
  if (search?.trim())    q.set('search', search.trim());
  if (branchId?.trim()) q.set('sucursalId', branchId.trim());
  return `?${q.toString()}`;
}

export const bodegaService = {
  list: (activeStatus: CatalogActiveStatus = 'all', search?: string, branchId?: string) =>
    get<WarehouseDto[]>(`/api/inventory/warehouses${listQuery(activeStatus, search, branchId)}`),

  getById: (id: string) => get<WarehouseDetailDto>(`/api/inventory/warehouses/${id}`),

  create: (body: WarehousePayload) =>
    post<WarehouseDto>('/api/inventory/warehouses', body),

  update: (id: string, body: WarehousePayload & { id: string }) =>
    put<WarehouseDto>(`/api/inventory/warehouses/${id}`, body),

  disable: (id: string) => patch<WarehouseDto>(`/api/inventory/warehouses/${id}/disable`),
  enable:  (id: string) => patch<WarehouseDto>(`/api/inventory/warehouses/${id}/enable`),
};
