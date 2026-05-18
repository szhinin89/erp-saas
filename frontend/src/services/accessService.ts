import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import type {
  BootstrapLoginRequest,
  BootstrapLoginResponse,
  MyPermissionsResponse,
  SessionMenuGroupDto,
  SessionResponse,
  SwitchTenantRequest,
} from '../types/access';

export const accessService = {
  async bootstrapLogin(req: BootstrapLoginRequest) {
    const { data } = await api.post<ApiResponse<BootstrapLoginResponse>>('/api/admin/iam/bootstrap-login', req);
    return data.responseObject;
  },

  async switchTenant(bootstrapToken: string, req: SwitchTenantRequest) {
    const { data } = await api.post<ApiResponse<SessionResponse>>('/api/admin/iam/switch-tenant', req, {
      headers: { Authorization: `Bearer ${bootstrapToken}` },
    });
    return data.responseObject;
  },

  async getMyPermissions() {
    const { data } = await api.get<ApiResponse<MyPermissionsResponse>>('/api/admin/iam/me/permissions');
    return data.responseObject;
  },

  /** Menú lateral resuelto (tenant → plan → global). Alias de GET /api/access/me/menu. */
  async getSessionMenu() {
    const { data } = await api.get<ApiResponse<SessionMenuGroupDto[]>>('/api/me/menu');
    return data.responseObject ?? [];
  },
};

