import type { NavigateFunction } from 'react-router-dom';
import { PLATFORM_UI } from '../modules/platform/api/platformApiPaths';

/** sessionStorage — ficha de suscriptor en panel platform. */
export const PLATFORM_DETAIL_SUBSCRIBER_STORAGE_KEY = 'erp.saas.platform.detailSubscriberId';

export function persistPlatformDetailSubscriberId(subscriberId: string): void {
  try {
    sessionStorage.setItem(PLATFORM_DETAIL_SUBSCRIBER_STORAGE_KEY, subscriberId.trim());
  } catch {
    /* storage disabled */
  }
}

export function clearPlatformDetailSubscriberId(): void {
  try {
    sessionStorage.removeItem(PLATFORM_DETAIL_SUBSCRIBER_STORAGE_KEY);
  } catch {
    /* */
  }
}

export function readPlatformDetailSubscriberId(): string | null {
  try {
    const v = sessionStorage.getItem(PLATFORM_DETAIL_SUBSCRIBER_STORAGE_KEY)?.trim();
    return v || null;
  } catch {
    return null;
  }
}

/** Navega a la ficha canónica del suscriptor (Platform shell). */
export function goToSubscriberDetail(navigate: NavigateFunction, subscriberId: string): void {
  persistPlatformDetailSubscriberId(subscriberId);
  navigate(PLATFORM_UI.subscriberDetail(subscriberId));
}
