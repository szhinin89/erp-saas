import type { NavigateFunction } from 'react-router-dom';

/** Clave en `sessionStorage` para el tenant cuya ficha se muestra en Empresas → Datos (no va en la URL). */
export const COMPANIES_DETAIL_TENANT_STORAGE_KEY = 'erp.saas.companies.detailSubscriberId';

/** Legacy: limpiar si quedó de la pestaña «Plan y módulos» eliminada. */
const COMPANIES_SUBSCRIPTION_LEGACY_STORAGE_KEY = 'erp.saas.companies.subscriptionSubscriberId';

export function persistCompaniesDetailSubscriberId(subscriberId: string): void {
  try {
    sessionStorage.setItem(COMPANIES_DETAIL_TENANT_STORAGE_KEY, subscriberId.trim());
  } catch {
    /* modo privado o storage deshabilitado */
  }
}

export function clearCompaniesDetailSubscriberId(): void {
  try {
    sessionStorage.removeItem(COMPANIES_DETAIL_TENANT_STORAGE_KEY);
  } catch {
    /* */
  }
}

export function readCompaniesDetailSubscriberId(): string | null {
  try {
    const v = sessionStorage.getItem(COMPANIES_DETAIL_TENANT_STORAGE_KEY)?.trim();
    return v || null;
  } catch {
    return null;
  }
}

export function clearCompaniesSubscriptionSubscriberId(): void {
  try {
    sessionStorage.removeItem(COMPANIES_SUBSCRIPTION_LEGACY_STORAGE_KEY);
  } catch {
    /* */
  }
}

/** Abre `/companies` y deja el id en `sessionStorage` para la pestaña Datos (sin query en la URL). */
export function goToCompaniesTenantDetail(navigate: NavigateFunction, subscriberId: string): void {
  clearCompaniesSubscriptionSubscriberId();
  persistCompaniesDetailSubscriberId(subscriberId);
  navigate('/companies');
}

/** Compatibilidad: antes abría la pestaña «Plan y módulos»; ahora abre la ficha en Datos. */
export function goToCompaniesTenantSubscription(navigate: NavigateFunction, subscriberId: string): void {
  goToCompaniesTenantDetail(navigate, subscriberId);
}
