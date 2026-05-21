import type { ReactNode } from 'react';
import { useEffect, useState } from 'react';
import { useAuthStore } from '../store/authStore';
import { restoreSessionFromCookie } from '../lib/session/restoreSessionFromCookie';
import { getAccessToken } from '../lib/session/authTokenMemory';

type Props = { children: ReactNode };

/**
 * Espera hidratación Zustand y restaura access token vía cookie httpOnly si hace falta.
 * No renderiza rutas hasta bootstrap completo (evita flash login/app).
 */
export function SessionBootstrap({ children }: Props) {
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (!hasHydrated) return;

    let cancelled = false;
    (async () => {
      if (isAuthenticated && !getAccessToken()) {
        await restoreSessionFromCookie();
      }
      if (!cancelled) setReady(true);
    })();

    return () => {
      cancelled = true;
    };
  }, [hasHydrated, isAuthenticated]);

  if (!hasHydrated || !ready) return null;

  return <>{children}</>;
}
