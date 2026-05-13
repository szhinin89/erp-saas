import { Route, Navigate } from 'react-router-dom';

/**
 * Redirecciones legacy bajo <AppLayout /> (el shell principal vive en `superAdminShellRoutes`).
 */
export function adminRoutes(superAdminPanelEnabled: boolean) {
  if (!superAdminPanelEnabled) {
    return [];
  }

  return [
    <Route key="superadmin-nav" path="/superadmin/navigation-menu" element={<Navigate to="/companies?mainNavigation=1" replace />} />,
  ];
}
