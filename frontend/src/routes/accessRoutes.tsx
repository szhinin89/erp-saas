import { Route, Navigate } from 'react-router-dom';
import { SecuritySettingsPage } from '../pages/SecuritySettingsPage';
import { TenantAccessPage } from '../pages/TenantAccessPage';
import { ProfilesPage } from '../pages/ProfilesPage';

export const accessRoutes = [
  // ── Admin ──────────────────────────────────────────────────────────────────
  <Route key="admin-security" path="/admin/security" element={<SecuritySettingsPage />} />,
  <Route key="admin-users"    path="/admin/users"    element={<TenantAccessPage />} />,
  <Route key="admin-roles"    path="/admin/roles"    element={<ProfilesPage />} />,
  // Legacy redirects
  <Route key="security-legacy" path="/security"  element={<Navigate to="/admin/security" replace />} />,
  <Route key="access-legacy"   path="/access"    element={<Navigate to="/admin/users" replace />} />,
  <Route key="profiles-legacy" path="/profiles"  element={<Navigate to="/admin/roles" replace />} />,
];
