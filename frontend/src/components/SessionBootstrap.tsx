import type { ReactNode } from 'react';
import { useEffect, useState } from 'react';
import { useAuthStore } from '../store/authStore';
import { restoreSessionFromCookie } from '../lib/session/restoreSessionFromCookie';

type Props = { children: ReactNode };

/**
 * Espera hidratación Zustand y restaura access token vía cookie httpOnly si hace falta.
 */
export function SessionBootstrap({ children }: Props) {
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (!hasHydrated) return;

    let cancelled = false;
    (async () => {
      if (isAuthenticated) {
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
