import { useAuthStore } from '../../store/authStore';

type DevSessionEvent = 'login' | 'refresh' | 'switch-company' | 'switch-subscriber';

/** Logs DEV sin tokens — solo contexto de suscriptor/empresa. */
export function logDevSessionContext(event: DevSessionEvent): void {
  if (!import.meta.env.DEV) return;
  const { user, companySessionVersion } = useAuthStore.getState();
  console.info('[erp.session]', {
    event,
    subscriberId: user?.subscriberId ?? null,
    companyId: user?.companyId ?? null,
    companySessionVersion,
    role: user?.role ?? null,
  });
}
