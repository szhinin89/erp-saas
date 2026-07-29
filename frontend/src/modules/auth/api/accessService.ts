import { api } from "../../lib/api";
import type { ApiResponse } from "../../../types/api";
import type { NavMenuGroupDto } from "../../../types/access";

export const accessService = {
  async getSessionMenu(): Promise<NavMenuGroupDto[]> {
    const { data } =
      await api.get<ApiResponse<NavMenuGroupDto[]>>("/api/v1/me/menu");
    return data.data ?? [];
  },
};
