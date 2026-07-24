import { useMemo } from 'react';
import { useAuthStore } from '../store/authStore';

export function useRuntimeMode(): RuntimeMode {
  const user = useAuthStore((s) => s.user);

  return useMemo(() => {
    if (!user) return 'unknown';
    if (user.companyId) return 'company';
    return 'unknown';
  }, [user]);
}

export type RuntimeMode = 'company' | 'unknown';
