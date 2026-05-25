import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';
import type {
  BootstrapLoginRequest,
  BootstrapLoginResponse,
  MyPermissionsResponse,
  SessionMenuGroupDto,
  SessionResponse,
  SwitchSubscriberRequest,
} from '../../../types/access';

export const accessService = {
  async bootstrapLogin(req: BootstrapLoginRequest) {
    const { data } = await api.post<ApiResponse<BootstrapLoginResponse>>('/api/admin/iam/bootstrap-login', req);
    return data.responseObject;
  },

  async bootstrapSwitchSubscriber(bootstrapToken: string, req: SwitchSubscriberRequest) {
    const { data } = await api.post<ApiResponse<SessionResponse>>('/api/admin/iam/bootstrap-switch-subscriber', req, {
      headers: { Authorization: `Bearer ${bootstrapToken}` },
    });
    return data.responseObject;
  },

  async getMyPermissions() {
    const { data } = await api.get<ApiResponse<MyPermissionsResponse>>('/api/admin/iam/me/permissions');
    return data.responseObject;
  },

  async getSessionMenu() {
    const { data } = await api.get<ApiResponse<SessionMenuGroupDto[]>>('/api/me/menu');
    return data.responseObject ?? [];
  },
};
