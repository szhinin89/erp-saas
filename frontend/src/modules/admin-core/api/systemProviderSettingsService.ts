import { api } from "../../lib/api";
import type { ApiResponse } from "../../../types/api";
import type {
  SystemProviderSettings,
  UpdateSystemProviderSettingsPayload,
} from "../../../types/systemProviderSettings";

/** Consume SystemProviderSettingsController (backend), protegido por policy PlatformAdmin. */
export const systemProviderSettingsService = {
  async get(): Promise<SystemProviderSettings | null> {
    const { data } = await api.get<ApiResponse<SystemProviderSettings>>(
      "/api/v1/system/provider-settings",
    );
    return data.data ?? null;
  },

  async update(
    payload: UpdateSystemProviderSettingsPayload,
  ): Promise<SystemProviderSettings> {
    const { data } = await api.put<ApiResponse<SystemProviderSettings>>(
      "/api/v1/system/provider-settings",
      payload,
    );
    return data.data;
  },
};
