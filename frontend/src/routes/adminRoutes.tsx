import { Route, Navigate } from 'react-router-dom';
import { SuperAdminPanelPage } from '../pages/SuperAdminPanelPage';

/**
 * Rutas del SuperAdmin Panel (restringidas a superadminPanel enabled).
 * Todas están bajo <ProtectedRoute /> y <AppLayout />.
 * 
 * Paths (cuando enabled):
 * - /superadmin (panel principal)
 * - /superadmin/forms (redirect a /superadmin)
 * - /superadmin/plans (redirect a /superadmin?tab=plans)
 * - /superadmin/growth (redirect a /superadmin)
 * - /superadmin/navigation-menu (redirect a /companies?mainNavigation=1)
 */
export function adminRoutes(superAdminPanelEnabled: boolean) {
  if (!superAdminPanelEnabled) {
    return [];
  }

  return [
    <Route key="superadmin" path="/superadmin" element={<SuperAdminPanelPage />} />,
    <Route key="superadmin-forms" path="/superadmin/forms" element={<Navigate to="/superadmin" replace />} />,
    <Route key="superadmin-plans" path="/superadmin/plans" element={<Navigate to="/superadmin?tab=plans" replace />} />,
    <Route key="superadmin-growth" path="/superadmin/growth" element={<Navigate to="/superadmin" replace />} />,
    <Route key="superadmin-nav" path="/superadmin/navigation-menu" element={<Navigate to="/companies?mainNavigation=1" replace />} />,
  ];
}
