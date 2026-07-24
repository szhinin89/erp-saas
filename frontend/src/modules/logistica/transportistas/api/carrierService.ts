import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export type Carrier = {
  id: string;
  identificationType: 'RUC' | 'CI' | 'PASSPORT';
  identificationNumber: string;
  legalName: string;
  licensePlate: string;
  phone: string | null;
  email: string | null;
  isActive: boolean;
};

export type CreateCarrierRequest = {
  carrierId?:          string;
  identificationType:  string;
  identificationNumber: string;
  legalName:           string;
  licensePlate:        string;
  phone?:              string | null;
  email?:              string | null;
};

function unwrap<T>(body: unknown): T {
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('data' in o && o.data !== undefined) return o.data as T;
    if ('Data' in o && o.Data !== undefined) return o.Data as T;
  }
  return body as T;
}

export const carrierService = {
  async getAll(search?: string, activeStatus = 'all'): Promise<Carrier[]> {
    const q = new URLSearchParams({ activeStatus });
    if (search?.trim()) q.set('search', search.trim());
    const raw = await api
      .get<ApiResponse<Carrier[]> | Record<string, unknown>>(`/api/v1/logistics/carriers?${q.toString()}`)
      .then(r => unwrap<Carrier[]>(r.data));
    return raw ?? [];
  },

  async create(payload: CreateCarrierRequest): Promise<Carrier> {
    return api
      .post<ApiResponse<Carrier> | Record<string, unknown>>('/api/v1/logistics/carriers', payload)
      .then(r => unwrap<Carrier>(r.data));
  },

  async update(id: string, payload: CreateCarrierRequest): Promise<Carrier> {
    return api
      .put<ApiResponse<Carrier> | Record<string, unknown>>(`/api/v1/logistics/carriers/${id}`, { ...payload, carrierId: id })
      .then(r => unwrap<Carrier>(r.data));
  },

  async disable(id: string): Promise<Carrier> {
    return api
      .patch<ApiResponse<Carrier> | Record<string, unknown>>(`/api/v1/logistics/carriers/${id}/disable`, {})
      .then(r => unwrap<Carrier>(r.data));
  },

  async enable(id: string): Promise<Carrier> {
    return api
      .patch<ApiResponse<Carrier> | Record<string, unknown>>(`/api/v1/logistics/carriers/${id}/enable`, {})
      .then(r => unwrap<Carrier>(r.data));
  },
};
