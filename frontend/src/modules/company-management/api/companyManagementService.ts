import { api } from "../../lib/api";
import type { ApiResponse } from "../../../types/api";
import type {
  CompanyCreatedResult,
  CompanyDetail,
  CreateCompanyPayload,
} from "../../../types/companyManagement";

export const companyManagementService = {
  async getCurrent(): Promise<CompanyDetail | null> {
    const { data } = await api.get<ApiResponse<CompanyDetail>>(
      "/api/v1/companies/current",
    );
    return data.data ?? null;
  },

  async create(payload: CreateCompanyPayload): Promise<CompanyCreatedResult> {
    const { data } = await api.post<ApiResponse<CompanyCreatedResult>>(
      "/api/v1/companies",
      payload,
    );
    return data.data;
  },
};
