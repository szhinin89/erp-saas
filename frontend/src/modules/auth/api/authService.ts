import { apiGet, apiPost } from '../../lib/apiEnvelope';
import type { AuthResponse, LoginRequest } from '../../../types/auth';
import type { AccessibleCompany } from '../../../types/access';

export const authService = {
  loginUser: (credentials: LoginRequest) => apiPost<AuthResponse>('/api/auth/login', credentials),

  loginPlatform: (credentials: LoginRequest) => apiPost<AuthResponse>('/api/platform/auth/login', credentials),

  refresh: (refreshToken?: string | null) =>
    apiPost<AuthResponse>('/api/auth/refresh', refreshToken ? { refreshToken } : {}),

  listMyCompanies: () => apiGet<AccessibleCompany[]>('/api/auth/my-companies').then((rows) => rows ?? []),

  switchCompany: (companyId: string) => apiPost<AuthResponse>('/api/auth/switch-company', { companyId }),
};
