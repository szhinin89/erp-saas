import { useAuthStore } from '../../../store/authStore';
import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
import { dashboardService } from '../api/dashboardService';

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
