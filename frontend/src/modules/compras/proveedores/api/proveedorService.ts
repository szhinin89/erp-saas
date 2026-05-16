import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export type Proveedor = {
  id: string;
  ruc: string;
  razonSocial: string;
  nombreComercial: string | null;
  contactoPrincipal: string | null;
  email: string | null;
  phone: string | null;
  address: string | null;
  website: string | null;
  category: string | null;
  status: 'activo' | 'pendiente' | 'inactivo';
  isActive: boolean;
  createdAt: string;
};

export type CreateProveedorRequest = {
  ruc: string;
  razonSocial: string;
  nombreComercial?: string | null;
  contactoPrincipal?: string | null;
  email?: string | null;
  phone?: string | null;
  address?: string | null;
  website?: string | null;
  category?: string | null;
  status: 'activo' | 'pendiente' | 'inactivo';
};

function readEnvelope<T>(body: unknown): T {
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('responseObject' in o && o.responseObject !== undefined) return o.responseObject as T;
    if ('ResponseObject' in o && o.ResponseObject !== undefined) return o.ResponseObject as T;
  }
  return body as T;
}

export const proveedorService = {
  async getAll(search?: string): Promise<Proveedor[]> {
    const q = new URLSearchParams({ activeStatus: 'all' });
    if (search?.trim()) q.set('search', search.trim());
    const raw = await api
      .get<ApiResponse<Proveedor[]> | Record<string, unknown>>(`/api/suppliers?${q.toString()}`)
      .then((r) => readEnvelope<Proveedor[]>(r.data));
    return raw ?? [];
  },

  async create(payload: CreateProveedorRequest): Promise<Proveedor> {
    return api
      .post<ApiResponse<Proveedor> | Record<string, unknown>>('/api/suppliers', payload)
      .then((r) => readEnvelope<Proveedor>(r.data));
  },

  async update(id: string, payload: CreateProveedorRequest): Promise<Proveedor> {
    return api
      .put<ApiResponse<Proveedor> | Record<string, unknown>>(`/api/suppliers/${id}`, { ...payload, id })
      .then((r) => readEnvelope<Proveedor>(r.data));
  },

  async setStatus(id: string, status: 'activo' | 'pendiente' | 'inactivo'): Promise<Proveedor> {
    return api
      .patch<ApiResponse<Proveedor> | Record<string, unknown>>(`/api/suppliers/${id}/status`, { status })
      .then((r) => readEnvelope<Proveedor>(r.data));
  },
};
