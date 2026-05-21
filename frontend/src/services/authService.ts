import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import type { AuthResponse, LoginRequest } from '../types/auth';
import type { AccessibleCompany } from '../types/access';

export const authService = {
  async loginUser(credentials: LoginRequest): Promise<AuthResponse> {
    const { data } = await api.post<ApiResponse<AuthResponse>>('/api/auth/login', credentials);
    return data.responseObject;
  },

  async loginPlatform(credentials: LoginRequest): Promise<AuthResponse> {
    const { data } = await api.post<ApiResponse<AuthResponse>>('/api/platform/auth/login', credentials);
    return data.responseObject;
  },

  async refresh(refreshToken?: string | null): Promise<AuthResponse> {
    const { data } = await api.post<ApiResponse<AuthResponse>>(
      '/api/auth/refresh',
      refreshToken ? { refreshToken } : {},
    );
    return data.responseObject;
  },

  async listMyCompanies(): Promise<AccessibleCompany[]> {
    const { data } = await api.get<ApiResponse<AccessibleCompany[]>>('/api/auth/my-companies');
    return data.responseObject ?? [];
  },

  async switchCompany(companyId: string): Promise<AuthResponse> {
    const { data } = await api.post<ApiResponse<AuthResponse>>('/api/auth/switch-company', { companyId });
    return data.responseObject;
  },
};
