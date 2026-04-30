import { api } from '../lib/api';
import type { ApiResponse } from '../types/api';

export type CompanyItem = { id: string; name: string; slug: string };

export type CreateCompanyWithAdminRequest = {
  tenantName: string;
  tenantSlug: string;
  ruc?: string | null;
  shortName?: string | null;
  tradeName?: string | null;
  dinardap?: string | null;
  logoUrl?: string | null;
  displayOrder?: number;
  priority?: number;
  adminFirstName: string;
  adminLastName: string;
  adminEmail: string;
  adminPassword: string;
  passwordResetMode?: number;
};

export const companyService = {
  list: () =>
    api.get<ApiResponse<{ tenants: CompanyItem[] }>>('/api/access/superadmin/tenants')
      .then((r) => r.data.responseObject.tenants),

  create: (req: CreateCompanyWithAdminRequest) =>
    api.post<ApiResponse<import('../types/access').SessionResponse>>('/api/access/superadmin/tenants', req)
      .then((r) => r.data.responseObject),
};

