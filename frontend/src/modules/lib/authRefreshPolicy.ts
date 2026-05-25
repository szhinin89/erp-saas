/** Rutas de auth que no deben disparar el interceptor de refresh (evita loops). */
export const PUBLIC_AUTH_PATHS = [
  '/api/auth/login',
  '/api/platform/auth/login',
  '/api/auth/register',
  '/api/auth/password-reset',
  '/api/auth/forgot-password',
  '/api/auth/reset-password',
  '/api/auth/refresh',
  '/api/auth/switch-subscriber',
  '/api/admin/iam/bootstrap-login',
  '/api/admin/iam/switch-subscriber',
  '/api/admin/iam/subscriber/company_user_memberships',
  '/api/admin/iam/register-subscriber',
] as const;

export function isPublicAuthPath(url: string): boolean {
  return PUBLIC_AUTH_PATHS.some((p) => url.includes(p));
}

/** true → intentar refresh; false → propagar error (sin loop en /api/auth/refresh). */
export function shouldAttemptTokenRefresh(
  status: number | undefined,
  url: string,
  alreadyRetried: boolean,
): boolean {
  if (status !== 401) return false;
  if (isPublicAuthPath(url)) return false;
  if (alreadyRetried) return false;
  return true;
}
