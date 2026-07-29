import { api } from "../../lib/api";
import type { ApiResponse } from "../../../types/api";
import type {
  MyAvailableBranchesDto,
  SessionBranchDto,
  SessionContextDto,
} from "../../../types/session";

export const sessionService = {
  async getContext(): Promise<SessionContextDto> {
    const { data } = await api.get<ApiResponse<SessionContextDto>>(
      "/api/v1/session/context",
    );
    return data.data;
  },

  async getAvailableBranches(): Promise<MyAvailableBranchesDto> {
    const { data } = await api.get<ApiResponse<MyAvailableBranchesDto>>(
      "/api/v1/session/available-branches",
    );
    return data.data;
  },

  async switchBranch(branchId: string): Promise<SessionBranchDto> {
    const { data } = await api.post<ApiResponse<SessionBranchDto>>(
      "/api/v1/session/switch-branch",
      { branchId },
    );
    return data.data;
  },
};
