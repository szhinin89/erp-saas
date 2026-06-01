import { useAuthStore } from '../../../store/authStore';
import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
import { dashboardService } from '../api/dashboardService';

export function useDashboardKpis() {
  const companyId            = useAuthStore((s) => s.user?.companyId);
  const needsOnboarding      = useAuthStore((s) => s.companyNeedsOnboarding);
  return useCompanyScopedAsync(
    () => dashboardService.getKpis(companyId!),
    !!companyId && !needsOnboarding,
  );
}

export function useArAging() {
  const companyId       = useAuthStore((s) => s.user?.companyId);
  const needsOnboarding = useAuthStore((s) => s.companyNeedsOnboarding);
  return useCompanyScopedAsync(
    () => dashboardService.getArAging(companyId!),
    !!companyId && !needsOnboarding,
  );
}

export function useApAging() {
  const companyId       = useAuthStore((s) => s.user?.companyId);
  const needsOnboarding = useAuthStore((s) => s.companyNeedsOnboarding);
  return useCompanyScopedAsync(
    () => dashboardService.getApAging(companyId!),
    !!companyId && !needsOnboarding,
  );
}
