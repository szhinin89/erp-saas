import { api } from "../../lib/api";
import type { ApiResponse } from "../../../types/api";
import type {
  CompanyDetail,
  CompanyListItem,
  CreateCompanyPayload,
  UpdateCompanyPayload,
} from "../../../types/companyManagement";

export const companyManagementService = {
  async list(activeOnly = true): Promise<CompanyListItem[]> {
    const { data } = await api.get<ApiResponse<CompanyListItem[]>>(
      "/api/v1/companies",
      {
        params: { activeOnly },
      },
    );
    return data.data ?? [];
  },

  async getCurrent(): Promise<CompanyDetail | null> {
    const { data } = await api.get<ApiResponse<CompanyDetail>>(
      "/api/v1/companies/current",
    );
    return data.data ?? null;
  },

  async getById(id: string): Promise<CompanyDetail> {
    const { data } = await api.get<ApiResponse<CompanyDetail>>(
      `/api/v1/companies/${id}`,
    );
    return data.data;
  },

  async create(payload: CreateCompanyPayload): Promise<CompanyDetail> {
    const { data } = await api.post<ApiResponse<CompanyDetail>>(
      "/api/v1/companies",
      payload,
    );
    return data.data;
  },

  async update(
    id: string,
    payload: UpdateCompanyPayload,
  ): Promise<CompanyDetail> {
    const { data } = await api.put<ApiResponse<CompanyDetail>>(
      `/api/v1/companies/${id}`,
      payload,
    );
    return data.data;
  },
};
