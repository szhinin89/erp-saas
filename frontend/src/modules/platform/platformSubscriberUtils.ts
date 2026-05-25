import { SUBSCRIBER_MODULE_KEYS } from '../../constants/subscriptionModules';

export function defaultModuleChecksAllOn(): Record<string, boolean> {
  const o: Record<string, boolean> = {};
  for (const k of SUBSCRIBER_MODULE_KEYS) o[k] = true;
  return o;
}

/** Si no restringe, o marca todos, equivale a sin JSON de módulos en backend. */
export function normalizeEnabledModulesForApi(
  restrict: boolean,
  checks: Record<string, boolean>,
): string[] | undefined {
  if (!restrict) return undefined;
  const selected = SUBSCRIBER_MODULE_KEYS.filter((k) => checks[k]);
  if (selected.length === 0) return undefined;
  if (selected.length === SUBSCRIBER_MODULE_KEYS.length) return undefined;
  return [...selected];
}

export function storeImpersonationSubscriberName(name: string) {
  localStorage.setItem('platform-impersonation-subscriber-name', name);
}

export function slugifySubscriberName(name: string) {
  return (name ?? '')
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/(^-|-$)/g, '')
    .slice(0, 64);
}
