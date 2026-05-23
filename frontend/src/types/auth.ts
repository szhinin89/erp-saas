export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  subscriberId: string;
  /** IAM unificado: Platform | Company | Subscriber */
  userType?: string | null;
  /** Rol platform cuando userType=Platform */
  platformRole?: string | null;
  /** Empresa operativa (RUC) activa en la sesión. */
  companyId?: string | null;
  token: string;
  /** Código comercial del plan del subscriber (p. ej. starter). */
  planCode?: string | null;
  /** Módulos contratados / efectivos para el subscriber (claves: catalog, accounting, saas, access). */
  enabledModules?: string[];
  /** Token opaco de larga duración para renovar el access token. Null para operador platform global. */
  refreshToken?: string | null;
  /** Fecha de expiración del refresh token (ISO 8601). */
  refreshTokenExpiry?: string | null;
}
