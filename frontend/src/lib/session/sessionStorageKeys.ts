/** Zustand persist — perfil de usuario (sin tokens). sessionStorage. */
export const AUTH_PROFILE_STORAGE_KEY = "auth-profile";

/** @deprecated Migración: clave legacy en localStorage; fullLogout la elimina. */
export const AUTH_STORAGE_KEY = "auth-storage";

/** Zustand persist — permisos y snapshot SaaS del suscriptor activo (sessionStorage). */
export const PERMISSIONS_STORAGE_KEY = "permissions-storage";

/** Zustand persist — bootstrap IAM multi-subscriber (sessionStorage). */
export const ACCESS_BOOTSTRAP_STORAGE_KEY = "access-bootstrap";

/** Prefijo de claves de contexto de navegación SaaS (sessionStorage). */
export const SAAS_SESSION_STORAGE_PREFIX = "erp.saas.";

/**
 * Zustand persist — sucursal activa de la sesión (Fase I-2). Bajo el prefijo erp.saas.* para
 * que fullLogout la elimine automáticamente vía el barrido genérico por prefijo, sin necesitar
 * una entrada explícita en clearPersistedSessionArtifacts.
 */
export const ACTIVE_BRANCH_STORAGE_KEY = `${SAAS_SESSION_STORAGE_PREFIX}company.activeBranchId`;
