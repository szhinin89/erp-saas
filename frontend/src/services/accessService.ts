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
    const { data } = await api.get<ApiResponse<MyPermissionsResponse>>('/api/access/me/permissions');
    return data.responseObject;
  },

  /** Menú lateral / cabecera definido en `ui_nav_groups` / `ui_nav_items`. */
  async getSessionMenu() {
    const { data } = await api.get<ApiResponse<SessionMenuGroupDto[]>>('/api/access/me/menu');
    return data.responseObject ?? [];
  },
};

