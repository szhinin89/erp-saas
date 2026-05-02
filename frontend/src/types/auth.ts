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
}
