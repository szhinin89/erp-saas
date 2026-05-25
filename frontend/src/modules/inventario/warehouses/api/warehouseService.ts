import { apiGet, apiPatch, apiPost, apiPut } from '../../../lib/apiEnvelope';

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

function listQuery(activeStatus: CatalogActiveStatus, search?: string, branchId?: string) {
  const q = new URLSearchParams();
  q.set('activeStatus', activeStatus);
  if (search?.trim()) q.set('search', search.trim());
  if (branchId?.trim()) q.set('sucursalId', branchId.trim());
  return `?${q.toString()}`;
}

export const warehouseService = {
  list: (activeStatus: CatalogActiveStatus = 'all', search?: string, branchId?: string) =>
    apiGet<WarehouseDto[]>(`/api/inventory/warehouses${listQuery(activeStatus, search, branchId)}`),

  getById: (id: string) => apiGet<WarehouseDetailDto>(`/api/inventory/warehouses/${id}`),

  create: (body: WarehousePayload) => apiPost<WarehouseDto>('/api/inventory/warehouses', body),

  update: (id: string, body: WarehousePayload & { id: string }) =>
    apiPut<WarehouseDto>(`/api/inventory/warehouses/${id}`, body),

  disable: (id: string) => apiPatch<WarehouseDto>(`/api/inventory/warehouses/${id}/disable`),

  enable: (id: string) => apiPatch<WarehouseDto>(`/api/inventory/warehouses/${id}/enable`),
};
