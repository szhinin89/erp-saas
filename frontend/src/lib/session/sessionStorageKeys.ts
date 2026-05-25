/** Zustand persist — perfil de usuario (sin tokens). sessionStorage. */
export const AUTH_PROFILE_STORAGE_KEY = 'auth-profile';

/** @deprecated Migración: clave legacy en localStorage; fullLogout la elimina. */
export const AUTH_STORAGE_KEY = 'auth-storage';

/** Zustand persist — permisos y snapshot SaaS del tenant activo (sessionStorage). */
export const PERMISSIONS_STORAGE_KEY = 'permissions-storage';

/** Zustand persist — bootstrap IAM multi-subscriber (sessionStorage). */
export const ACCESS_BOOTSTRAP_STORAGE_KEY = 'access-bootstrap';

/** Etiqueta UI de impersonación platform (no es credencial). */
export const PLATFORM_IMPERSONATION_NAME_KEY = 'platform-impersonation-subscriber-name';

/** Prefijo de claves de contexto de navegación SaaS (sessionStorage). */
export const SAAS_SESSION_STORAGE_PREFIX = 'erp.saas.';

export function readPlatformImpersonationName(): string | null {
  try {
    return localStorage.getItem(PLATFORM_IMPERSONATION_NAME_KEY)?.trim() || null;
  } catch {
    return null;
  }
}

export function clearPlatformImpersonationName(): void {
  try {
    localStorage.removeItem(PLATFORM_IMPERSONATION_NAME_KEY);
  } catch {
    /* storage disabled */
  }
}
