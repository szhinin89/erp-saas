import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import type { EntitlementsSnapshot } from '../types/entitlements';

export const entitlementsService = {
  async getMe(): Promise<EntitlementsSnapshot> {
    const { data } = await api.get<ApiResponse<EntitlementsSnapshot>>('/api/subscribers/entitlements/me');
    const snap = data.responseObject;
    return {
      planCode: snap?.planCode ?? null,
      planName: snap?.planName ?? null,
      enabledModules: snap?.enabledModules ?? [],
      enabledFeatures: snap?.enabledFeatures ?? [],
      limits: snap?.limits ?? {},
      hasModuleRestrictions: snap?.hasModuleRestrictions ?? true,
    };
  },
};
