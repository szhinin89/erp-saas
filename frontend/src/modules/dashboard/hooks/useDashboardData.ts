import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
import { dashboardService } from '../api/dashboardService';

export function useDashboardKpis() {
  return useCompanyScopedAsync(
    () => dashboardService.getKpis(),
    true,
  );
}