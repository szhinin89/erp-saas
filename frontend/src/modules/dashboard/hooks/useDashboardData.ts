import { useAuthStore } from '../../../store/authStore';
import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
import { dashboardService } from '../api/dashboardService';

// ProtectedRoute guarantees Dashboard only mounts when onboardingCompleted=true.
// No additional onboarding checks needed here.

export function useDashboardKpis() {
  const companyId = useAuthStore((s) => s.user?.companyId);
  return useCompanyScopedAsync(
    () => dashboardService.getKpis(companyId!),
    !!companyId,
  );
}

export function useArAging() {
  const companyId = useAuthStore((s) => s.user?.companyId);
  return useCompanyScopedAsync(
    () => dashboardService.getArAging(companyId!),
    !!companyId,
  );
}

export function useApAging() {
  const companyId = useAuthStore((s) => s.user?.companyId);
  return useCompanyScopedAsync(
    () => dashboardService.getApAging(companyId!),
    !!companyId,
  );
}
