import type { NavigateFunction } from 'react-router-dom';
import { PLATFORM_UI } from '../modules/platform/api/platformApiPaths';

/** sessionStorage compat (clave estable desde redirect `/companies`). */
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

/** Navega a la ficha canónica del suscriptor (Platform shell). */
export function goToSubscriberDetail(navigate: NavigateFunction, subscriberId: string): void {
  clearCompaniesSubscriptionSubscriberId();
  persistCompaniesDetailSubscriberId(subscriberId);
  navigate(PLATFORM_UI.subscriberDetail(subscriberId));
}
