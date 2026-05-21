import { apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';

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

function listQuery(activeStatus: CatalogActiveStatus, search?: string) {
  const q = new URLSearchParams();
  q.set('activeStatus', activeStatus);
  if (search?.trim()) q.set('search', search.trim());
  return `?${q.toString()}`;
}

/** API catálogo — DTO alineado al contrato `/api/sales/customers` (forma legalName/tradeName). */
export const customerCatalogService = {
  list: (activeStatus: CatalogActiveStatus = 'all', search?: string) =>
    apiGet<CustomerDto[]>(`/api/sales/customers${listQuery(activeStatus, search)}`),

  getById: (id: string) => apiGet<CustomerDetailDto>(`/api/sales/customers/${id}`),

  create: (body: CreateCustomerBody) => apiPost<CustomerDto>('/api/sales/customers', body),

  update: (id: string, body: UpdateCustomerBody) => apiPut<CustomerDto>(`/api/sales/customers/${id}`, body),

  disable: (id: string) => apiPatch<CustomerDto>(`/api/sales/customers/${id}/disable`),

  enable: (id: string) => apiPatch<CustomerDto>(`/api/sales/customers/${id}/enable`),
};

/** @deprecated Alias legacy — preferir `customerCatalogService`. */
export const customerService = customerCatalogService;
