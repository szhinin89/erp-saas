import { SuperAdminPanelPage } from '../SuperAdminPanelPage';

/** Resumen / KPIs del panel global (misma vista que la pestaña «Resumen»). */
export function SuperAdminOverviewPage() {
  return <SuperAdminPanelPage shellLayout embeddedTab="overview" />;
}
