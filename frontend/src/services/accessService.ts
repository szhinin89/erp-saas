import { api } from '../lib/api';
import type { ApiResponse } from '../types/api';
import type {
  BootstrapLoginRequest,
  BootstrapLoginResponse,
  SessionResponse,
  SwitchTenantRequest,
} from '../types/access';

export const accessService = {
  async bootstrapLogin(req: BootstrapLoginRequest) {
    const { data } = await api.post<ApiResponse<BootstrapLoginResponse>>('/api/access/bootstrap-login', req);
    return data.responseObject;
  },

  async switchTenant(bootstrapToken: string, req: SwitchTenantRequest) {
    const { data } = await api.post<ApiResponse<SessionResponse>>('/api/access/switch-tenant', req, {
      headers: { Authorization: `Bearer ${bootstrapToken}` },
    });
    return data.responseObject;
  },

  async getMyPermissions() {
    const { data } = await api.get<ApiResponse<{ permissions: string[] }>>('/api/access/me/permissions');
    return data.responseObject;
  },
};

