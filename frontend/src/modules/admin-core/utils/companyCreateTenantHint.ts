import { SAAS_SESSION_STORAGE_PREFIX } from "../../../lib/session/sessionStorageKeys";

/**
 * SEC-02 — contexto de navegación dashboard global → crear empresa, fuera de la URL.
 *
 * Solo es una sugerencia de UX para preseleccionar el tenant: nunca es autoridad. Quien lo lee
 * debe validarlo contra el listado de tenants que devuelve la API y el backend autoriza el alta.
 * Bajo el prefijo erp.saas.* para que fullLogout lo elimine con el barrido genérico.
 */
export const COMPANY_CREATE_TENANT_HINT_KEY = `${SAAS_SESSION_STORAGE_PREFIX}adminCore.companyCreate.tenantHint`;

/** Vigencia del hint: cubre la navegación inmediata, no un retorno posterior a la pantalla. */
export const COMPANY_CREATE_TENANT_HINT_TTL_MS = 5 * 60 * 1000;

interface StoredTenantHint {
  tenantId: string;
  savedAtMs: number;
}

export function saveCompanyCreateTenantHint(tenantId: string): void {
  try {
    const value: StoredTenantHint = { tenantId, savedAtMs: Date.now() };
    sessionStorage.setItem(COMPANY_CREATE_TENANT_HINT_KEY, JSON.stringify(value));
  } catch {
    // Sin storage disponible la pantalla funciona igual, solo sin preselección.
  }
}

/**
 * Devuelve el tenantId sugerido si es vigente y bien formado; si está vencido o malformado lo
 * descarta. No lo elimina cuando es válido: el consumidor llama a clear tras aplicarlo, para que
 * un efecto cancelado (p. ej. doble montaje de StrictMode) no pierda el hint.
 */
export function readCompanyCreateTenantHint(): string | null {
  try {
    const raw = sessionStorage.getItem(COMPANY_CREATE_TENANT_HINT_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<StoredTenantHint> | null;
    const age = Date.now() - Number(parsed?.savedAtMs);
    if (
      typeof parsed?.tenantId !== "string" ||
      !parsed.tenantId ||
      !(age >= 0 && age <= COMPANY_CREATE_TENANT_HINT_TTL_MS)
    ) {
      clearCompanyCreateTenantHint();
      return null;
    }
    return parsed.tenantId;
  } catch {
    clearCompanyCreateTenantHint();
    return null;
  }
}

export function clearCompanyCreateTenantHint(): void {
  try {
    sessionStorage.removeItem(COMPANY_CREATE_TENANT_HINT_KEY);
  } catch {
    // Nada que limpiar si el storage no está disponible.
  }
}
