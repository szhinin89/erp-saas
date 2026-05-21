import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type CustomerDto = {
  id: string;
  identificationType: string;
  identificationNumber: string;
  legalName: string;
  tradeName: string | null;
  addressLine: string | null;
  phone: string | null;
  email: string | null;
  notes: string | null;
  isActive: boolean;
};

export type CustomerDetailDto = CustomerDto & {
  createdAt: string;
  updatedAt: string | null;
  createdBy: string;
  updatedBy: string | null;
};

export type CatalogActiveStatus = 'all' | 'active' | 'inactive';

function readEnvelopePayload<T>(body: unknown): T {
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('responseObject' in o && o.responseObject !== undefined) return o.responseObject as T;
    if ('ResponseObject' in o && o.ResponseObject !== undefined) return o.ResponseObject as T;
  }
  return body as T;
}

function get<T>(url: string) {
  return api.get<ApiResponse<T> | Record<string, unknown>>(url).then((r) => readEnvelopePayload<T>(r.data));
}

function post<T>(url: string, body: unknown) {
  return api.post<ApiResponse<T> | Record<string, unknown>>(url, body).then((r) => readEnvelopePayload<T>(r.data));
}

function put<T>(url: string, body: unknown) {
  return api.put<ApiResponse<T> | Record<string, unknown>>(url, body).then((r) => readEnvelopePayload<T>(r.data));
}

function patch<T>(url: string) {
  return api.patch<ApiResponse<T> | Record<string, unknown>>(url, {}).then((r) => readEnvelopePayload<T>(r.data));
}

function listQuery(activeStatus: CatalogActiveStatus, search?: string) {
  const q = new URLSearchParams();
  q.set('activeStatus', activeStatus);
  if (search?.trim()) q.set('search', search.trim());
  return `?${q.toString()}`;
}

export type CreateCustomerBody = {
  identificationType: string;
  identificationNumber: string;
  legalName: string;
  tradeName: string | null;
  addressLine: string | null;
  phone: string | null;
  email: string | null;
  notes: string | null;
  isActive: boolean;
};

export type UpdateCustomerBody = CreateCustomerBody & { id: string };

export const customerService = {
  list: (activeStatus: CatalogActiveStatus = 'all', search?: string) =>
    get<CustomerDto[]>(`/api/sales/customers${listQuery(activeStatus, search)}`),

  getById: (id: string) => get<CustomerDetailDto>(`/api/sales/customers/${id}`),

  create: (body: CreateCustomerBody) => post<CustomerDto>('/api/sales/customers', body),

  update: (id: string, body: UpdateCustomerBody) => put<CustomerDto>(`/api/sales/customers/${id}`, body),

  disable: (id: string) => patch<CustomerDto>(`/api/sales/customers/${id}/disable`),
  enable: (id: string) => patch<CustomerDto>(`/api/sales/customers/${id}/enable`),
};
