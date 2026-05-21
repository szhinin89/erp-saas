import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';
import type {
  CompanyDetail,
  CompanyListItem,
  CreateCompanyPayload,
  UpdateCompanyPayload,
} from '../../../types/companyManagement';

export const companyManagementService = {
  async list(activeOnly = true): Promise<CompanyListItem[]> {
    const { data } = await api.get<ApiResponse<CompanyListItem[]>>('/api/companies', {
      params: { activeOnly },
    });
    return data.responseObject ?? [];
  },

  async getCurrent(): Promise<CompanyDetail | null> {
    const { data } = await api.get<ApiResponse<CompanyDetail>>('/api/companies/current');
    return data.responseObject ?? null;
  },

  async getById(id: string): Promise<CompanyDetail> {
    const { data } = await api.get<ApiResponse<CompanyDetail>>(`/api/companies/${id}`);
    return data.responseObject;
  },

  async create(payload: CreateCompanyPayload): Promise<CompanyDetail> {
    const { data } = await api.post<ApiResponse<CompanyDetail>>('/api/companies', payload);
    return data.responseObject;
  },

  async update(id: string, payload: UpdateCompanyPayload): Promise<CompanyDetail> {
    const { data } = await api.put<ApiResponse<CompanyDetail>>(`/api/companies/${id}`, payload);
    return data.responseObject;
  },
};
