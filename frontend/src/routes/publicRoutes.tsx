import { Route } from 'react-router-dom';
import { LoginPage } from '../pages/LoginPage';
import { PasswordResetPage } from '../pages/PasswordResetPage';
import { TenantSelectPage } from '../pages/TenantSelectPage';

/**
 * Rutas públicas (sin autenticación requerida).
 * Estas rutas NO están dentro de <ProtectedRoute /> ni <AppLayout />.
 */
export const publicRoutes = [
  <Route key="login" path="/login" element={<LoginPage />} />,
  <Route key="password-reset" path="/password-reset" element={<PasswordResetPage />} />,
  <Route key="select-tenant" path="/select-tenant" element={<TenantSelectPage />} />,
];
