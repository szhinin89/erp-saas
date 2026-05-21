import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export type Supplier = {
  id: string;
  taxId: string;
  legalName: string;
  tradeName: string | null;
  primaryContact: string | null;
  email: string | null;
  phone: string | null;
  address: string | null;
  website: string | null;
  category: string | null;
  status: 'active' | 'pending' | 'inactive';
  isActive: boolean;
  createdAt: string;
};

export type CreateSupplierRequest = {
  taxId: string;
  legalName: string;
  tradeName?: string | null;
  primaryContact?: string | null;
  email?: string | null;
  phone?: string | null;
  address?: string | null;
  website?: string | null;
  category?: string | null;
  status: 'active' | 'pending' | 'inactive';
};

function readEnvelope<T>(body: unknown): T {
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('responseObject' in o && o.responseObject !== undefined) return o.responseObject as T;
    if ('ResponseObject' in o && o.ResponseObject !== undefined) return o.ResponseObject as T;
  }
  return body as T;
}

export const supplierService = {
  async getAll(search?: string): Promise<Supplier[]> {
    const q = new URLSearchParams({ activeStatus: 'all' });
    if (search?.trim()) q.set('search', search.trim());
    const raw = await api
      .get<ApiResponse<Supplier[]> | Record<string, unknown>>(`/api/purchases/suppliers?${q.toString()}`)
      .then((r) => readEnvelope<Supplier[]>(r.data));
    return raw ?? [];
  },

  async create(payload: CreateSupplierRequest): Promise<Supplier> {
    return api
      .post<ApiResponse<Supplier> | Record<string, unknown>>('/api/purchases/suppliers', payload)
      .then((r) => readEnvelope<Supplier>(r.data));
  },

  async update(id: string, payload: CreateSupplierRequest): Promise<Supplier> {
    return api
      .put<ApiResponse<Supplier> | Record<string, unknown>>(`/api/purchases/suppliers/${id}`, { ...payload, id })
      .then((r) => readEnvelope<Supplier>(r.data));
  },

  async setStatus(id: string, status: 'active' | 'pending' | 'inactive'): Promise<Supplier> {
    const action = status === 'inactive' ? 'disable' : 'enable';
    return api
      .patch<ApiResponse<Supplier> | Record<string, unknown>>(`/api/purchases/suppliers/${id}/${action}`, {})
      .then((r) => readEnvelope<Supplier>(r.data));
  },
};
