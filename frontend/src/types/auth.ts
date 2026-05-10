export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  tenantId: string;
  token: string;
  /** Código comercial del plan del tenant (p. ej. starter). */
  planCode?: string | null;
  /** Módulos contratados / efectivos para el tenant (claves: catalog, accounting, saas, access). */
  enabledModules?: string[];
  /** Token opaco de larga duración para renovar el access token. Null para SuperAdmin. */
  refreshToken?: string | null;
  /** Fecha de expiración del refresh token (ISO 8601). */
  refreshTokenExpiry?: string | null;
}
