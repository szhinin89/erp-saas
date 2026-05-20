import type { NavigateFunction } from 'react-router-dom';

/** Clave en `sessionStorage` para el subscriber cuya ficha se muestra en SuperAdmin → Empresas → Datos. */
export const COMPANIES_DETAIL_SUBSCRIBER_STORAGE_KEY = 'erp.saas.companies.detailSubscriberId';

const COMPANIES_SUBSCRIPTION_LEGACY_STORAGE_KEY = 'erp.saas.companies.subscriptionSubscriberId';

export function persistCompaniesDetailSubscriberId(subscriberId: string): void {
  try {
    sessionStorage.setItem(COMPANIES_DETAIL_SUBSCRIBER_STORAGE_KEY, subscriberId.trim());
  } catch {
    /* storage disabled */
  }
}

export function clearCompaniesDetailSubscriberId(): void {
  try {
    sessionStorage.removeItem(COMPANIES_DETAIL_SUBSCRIBER_STORAGE_KEY);
  } catch {
    /* */
  }
}

export function readCompaniesDetailSubscriberId(): string | null {
  try {
    const v = sessionStorage.getItem(COMPANIES_DETAIL_SUBSCRIBER_STORAGE_KEY)?.trim();
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

/** Abre `/companies` con el subscriber seleccionado en la pestaña Datos. */
export function goToCompaniesSubscriberDetail(navigate: NavigateFunction, subscriberId: string): void {
  clearCompaniesSubscriptionSubscriberId();
  persistCompaniesDetailSubscriberId(subscriberId);
  navigate('/companies');
}

/** @deprecated Use `goToCompaniesSubscriberDetail`. */
export function goToCompaniesTenantDetail(navigate: NavigateFunction, subscriberId: string): void {
  goToCompaniesSubscriberDetail(navigate, subscriberId);
}
