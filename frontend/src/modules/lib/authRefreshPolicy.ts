/** Rutas de auth que no deben disparar el interceptor de refresh (evita loops). */
export const PUBLIC_AUTH_PATHS = [
  "/api/v1/auth/login",
  "/api/v1/auth/forgot-password",
  "/api/v1/auth/reset-password",
  "/api/v1/auth/refresh",
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
