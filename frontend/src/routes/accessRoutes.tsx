import { Route, Navigate } from 'react-router-dom';
import { lazyNamedPage } from './lazyPage';

const SecuritySettingsPage = lazyNamedPage(
  () => import('../modules/security/pages/SecuritySettingsPage'),
  'SecuritySettingsPage',
);
const SubscriberAccessPage = lazyNamedPage(
  () => import('../modules/access/pages/SubscriberAccessPage'),
  'SubscriberAccessPage',
);
const ProfilesPage = lazyNamedPage(() => import('../modules/access/pages/ProfilesPage'), 'ProfilesPage');

export const accessRoutes = [
  <Route key="admin-security" path="/admin/security" element={<SecuritySettingsPage />} />,
  <Route key="admin-users" path="/admin/users" element={<SubscriberAccessPage />} />,
  <Route key="admin-roles" path="/admin/roles" element={<ProfilesPage />} />,
  <Route key="security-legacy" path="/security" element={<Navigate to="/admin/security" replace />} />,
  <Route key="access-legacy" path="/access" element={<Navigate to="/admin/users" replace />} />,
  <Route key="profiles-legacy" path="/profiles" element={<Navigate to="/admin/roles" replace />} />,
];
