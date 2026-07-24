/**
 * Operational readiness of a company — mirrors backend CompanyOperationalStatus enum.
 * Numeric values are stable against backend renames (do NOT use string names).
 */
export const CompanyOperationalStatus = {
  PendingSetup: 1,
  Operational:  2,
  Suspended:    3,
} as const;
export type CompanyOperationalStatus = typeof CompanyOperationalStatus[keyof typeof CompanyOperationalStatus];

export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthResponse {
  userId: string;
  fullName: string;
  username: string;
  /** Dato de contacto opcional — ya no es la identidad de login (Fase G). */
  email: string | null;
  role: string;
  tenantId: string;
  /** Empresa operativa activa en la sesión. Null si RequiresCompanySelection=true. */
  companyId?: string | null;
  /** True cuando el usuario tiene múltiples empresas y debe seleccionar una antes de acceder al ERP. */
  requiresCompanySelection?: boolean;
  token: string;
  /** Token opaco para renovar el access token. */
  refreshToken?: string | null;
  /** Fecha de expiración del refresh token (ISO 8601). */
  refreshTokenExpiry?: string | null;
  onboardingCompleted?: boolean | null;
  operationalStatus?: CompanyOperationalStatus | null;
  /**
   * Fase H — true cuando el usuario debe cambiar su contraseña antes de obtener sesión: `token`
   * viene vacío y no debe tratarse como una sesión real (no llamar authStore.login() con esto).
   */
  requiresPasswordReset?: boolean;
  /** Token opaco de un solo uso, presente solo cuando requiresPasswordReset=true. */
  passwordResetToken?: string | null;
  /** Segundos de vigencia de passwordResetToken desde esta respuesta. */
  passwordResetTokenExpiresIn?: number | null;
}
