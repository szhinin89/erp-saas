import { apiGet, apiPost, readEnvelopePayload } from '../../lib/apiEnvelope';
import { normalizeAuthResponse } from '../normalizeAuthResponse';
import type { AuthResponse, LoginRequest } from '../../../types/auth';
import type { AccessibleCompany } from '../../../types/access';

function mapAuthResponse(raw: unknown): AuthResponse {
  return normalizeAuthResponse(
    raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : null,
  );
}

export const authService = {
  loginUser: (credentials: LoginRequest) =>
    apiPost<Record<string, unknown>>('/api/auth/login', credentials).then(mapAuthResponse),

  loginPlatform: (credentials: LoginRequest) =>
    apiPost<Record<string, unknown>>('/api/platform/auth/login', credentials).then(mapAuthResponse),

  refresh: (refreshToken?: string | null) =>
    apiPost<Record<string, unknown>>('/api/auth/refresh', refreshToken ? { refreshToken } : {}).then(mapAuthResponse),

  listMyCompanies: () => apiGet<AccessibleCompany[]>('/api/auth/my-companies').then((rows) => rows ?? []),

  switchCompany: (companyId: string) =>
    apiPost<Record<string, unknown>>('/api/auth/switch-company', { companyId }).then(mapAuthResponse),
};
