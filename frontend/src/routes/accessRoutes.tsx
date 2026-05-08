import { Route } from 'react-router-dom';
import { SecuritySettingsPage } from '../pages/SecuritySettingsPage';
import { TenantAccessPage } from '../pages/TenantAccessPage';
import { ProfilesPage } from '../pages/ProfilesPage';

/**
 * Rutas de Acceso y Seguridad.
 * Todas están bajo <ProtectedRoute /> y <AppLayout />.
 * 
 * Paths:
 * - /security (configuración de seguridad del tenant)
 * - /access (asignación de acceso a tenants)
 * - /profiles (perfiles de acceso)
 */
export const accessRoutes = [
  <Route key="security" path="/security" element={<SecuritySettingsPage />} />,
  <Route key="access" path="/access" element={<TenantAccessPage />} />,
  <Route key="profiles" path="/profiles" element={<ProfilesPage />} />,
];
