import { api } from "../../lib/api";
import type { ApiResponse } from "../../../types/api";
import type { AdminCoreCompany } from "../../../types/adminCore";

export const adminCoreService = {
  async listCompanies(): Promise<AdminCoreCompany[]> {
    const { data } = await api.get<ApiResponse<AdminCoreCompany[]>>(
      "/api/v1/admin-core/companies",
    );
    return data.data ?? [];
  },
};
