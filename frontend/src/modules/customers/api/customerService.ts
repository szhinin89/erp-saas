import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';

export type Customer = {
  id: string;
  identificationType: 'RUC' | 'CI';
  identificationNumber: string;
  fullName: string;
  email: string | null;
  phone: string | null;
  address: string | null;
  isActive: boolean;
};

export type CreateCustomerRequest = {
  identification: string;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  address?: string | null;
  isActive?: boolean;
};

type LegacyCustomerApi = {
  id: string;
  identificationType: string;
  identificationNumber: string;
  legalName: string;
  email: string | null;
  phone: string | null;
  addressLine: string | null;
  isActive: boolean;
};

function readEnvelopePayload<T>(body: unknown): T {
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('responseObject' in o && o.responseObject !== undefined) return o.responseObject as T;
    if ('ResponseObject' in o && o.ResponseObject !== undefined) return o.ResponseObject as T;
  }
  return body as T;
}

function toCustomer(item: LegacyCustomerApi): Customer {
  return {
    id: item.id,
    identificationType: item.identificationType.toUpperCase() === 'CI' ? 'CI' : 'RUC',
    identificationNumber: item.identificationNumber,
    fullName: item.legalName,
    email: item.email,
    phone: item.phone,
    address: item.addressLine,
    isActive: item.isActive,
  };
}

export const customerService = {
  async getAll(search?: string): Promise<Customer[]> {
    const query = new URLSearchParams();
    query.set('activeStatus', 'all');
    if (search?.trim()) query.set('search', search.trim());
    const raw = await api
      .get<ApiResponse<LegacyCustomerApi[]> | Record<string, unknown>>(`/api/customers?${query.toString()}`)
      .then((r) => readEnvelopePayload<LegacyCustomerApi[]>(r.data));
    return (raw ?? []).map(toCustomer);
  },

  async create(payload: CreateCustomerRequest): Promise<Customer> {
    const raw = await api
      .post<ApiResponse<LegacyCustomerApi> | Record<string, unknown>>('/api/customers', toApiPayload(payload, true))
      .then((r) => readEnvelopePayload<LegacyCustomerApi>(r.data));

    return toCustomer(raw);
  },

  async update(id: string, payload: CreateCustomerRequest): Promise<Customer> {
    const raw = await api
      .put<ApiResponse<LegacyCustomerApi> | Record<string, unknown>>(`/api/customers/${id}`, {
        ...toApiPayload(payload, true),
        id,
      })
      .then((r) => readEnvelopePayload<LegacyCustomerApi>(r.data));

    return toCustomer(raw);
  },

  async setActive(id: string, isActive: boolean): Promise<Customer> {
    const raw = await api
      .patch<ApiResponse<LegacyCustomerApi> | Record<string, unknown>>(
        `/api/customers/${id}/${isActive ? 'enable' : 'disable'}`,
        {}
      )
      .then((r) => readEnvelopePayload<LegacyCustomerApi>(r.data));

    return toCustomer(raw);
  },
};

function toApiPayload(payload: CreateCustomerRequest, defaultActive: boolean) {
  const identification = payload.identification.trim();
  const identificationType: 'RUC' | 'CI' = identification.length > 10 ? 'RUC' : 'CI';
  return {
    identificationType,
    identificationNumber: identification,
    legalName: payload.fullName.trim(),
    tradeName: null,
    addressLine: payload.address?.trim() || null,
    phone: payload.phone?.trim() || null,
    email: payload.email?.trim() || null,
    notes: null,
    isActive: payload.isActive ?? defaultActive,
  };
}
