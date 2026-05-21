/** Zustand persist — auth (access + refresh tokens, user profile). */
export const AUTH_STORAGE_KEY = 'auth-storage';

/** Zustand persist — permisos y snapshot SaaS del tenant activo. */
export const PERMISSIONS_STORAGE_KEY = 'permissions-storage';

/** Zustand persist — bootstrap IAM multi-subscriber (pre-switch). */
export const ACCESS_BOOTSTRAP_STORAGE_KEY = 'access-bootstrap';

/** Etiqueta UI de impersonación SuperAdmin (no es credencial). */
export const SUPERADMIN_IMPERSONATION_NAME_KEY = 'superadmin-impersonation-subscriber-name';

/** Prefijo de claves de contexto de navegación SaaS (sessionStorage). */
export const SAAS_SESSION_STORAGE_PREFIX = 'erp.saas.';
