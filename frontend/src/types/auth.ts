/**
 * Operational readiness of a company — mirrors backend CompanyOperationalStatus enum.
 * Numeric values are stable against backend renames (do NOT use string names).
 */
export enum CompanyOperationalStatus {
  PendingSetup = 1,
  Operational  = 2,
  Suspended    = 3,
}

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
  /** Empresa operativa activa en la sesión. Null si RequiresCompanySelection=true o platform operator. */
  companyId?: string | null;
  /** True cuando el usuario tiene múltiples empresas y debe seleccionar una antes de acceder al ERP. */
  requiresCompanySelection?: boolean;
  token: string;
  /** Código comercial del plan del subscriber (p. ej. starter). */
  planCode?: string | null;
  /** Módulos contratados / efectivos para el subscriber. */
  enabledModules?: string[];
  /** Token opaco para renovar el access token. */
  refreshToken?: string | null;
  /** Fecha de expiración del refresh token (ISO 8601). */
  refreshTokenExpiry?: string | null;
  /**
   * True cuando la empresa completó el wizard de onboarding (datos fiscales + infraestructura ERP).
   * False cuando el onboarding está pendiente — el usuario NO debe acceder al ERP.
   * Null/undefined cuando no hay empresa seleccionada (platform operator o multi-empresa pendiente).
   *
   * ProtectedRoute usa este campo para decidir ERP vs /onboarding/company ANTES de renderizar
   * cualquier componente ERP, eliminando la dependencia del 403 company_onboarding_required.
   */
  onboardingCompleted?: boolean | null;
  /**
   * Estado operativo de la empresa activa (enum CompanyOperationalStatus).
   * Null cuando no hay empresa seleccionada.
   */
  operationalStatus?: CompanyOperationalStatus | null;
}
