import type { NavigateFunction } from 'react-router-dom';
import { SUPERADMIN_UI } from '../modules/superadmin/api/platformApiPaths';

/** Clave legacy en `sessionStorage` (compat redirect desde `/companies`). */
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

/** Abre la ficha canónica del suscriptor en Super Admin. */
export function goToSubscriberDetail(navigate: NavigateFunction, subscriberId: string): void {
  clearCompaniesSubscriptionSubscriberId();
  persistCompaniesDetailSubscriberId(subscriberId);
  navigate(SUPERADMIN_UI.subscriberDetail(subscriberId));
}

/** @deprecated Usar goToSubscriberDetail — mantiene compat con callers legacy. */
export function goToCompaniesSubscriberDetail(navigate: NavigateFunction, subscriberId: string): void {
  goToSubscriberDetail(navigate, subscriberId);
}
