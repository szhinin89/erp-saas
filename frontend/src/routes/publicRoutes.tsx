import { Route } from 'react-router-dom';
import { LoginPage } from '../pages/LoginPage';
import { ForgotPasswordPage } from '../pages/ForgotPasswordPage';
import { ResetPasswordPage } from '../pages/ResetPasswordPage';
import { PasswordResetPage } from '../pages/PasswordResetPage';
import { SubscriberSelectPage } from '../pages/SubscriberSelectPage';
import { CompanySelectPage } from '../pages/CompanySelectPage';

/**
 * Rutas públicas (sin autenticación requerida).
 * Estas rutas NO están dentro de <ProtectedRoute /> ni <AppLayout />.
 */
export const publicRoutes = [
  <Route key="login" path="/login" element={<LoginPage />} />,
  <Route key="forgot-password" path="/forgot-password" element={<ForgotPasswordPage />} />,
  <Route key="reset-password" path="/reset-password" element={<ResetPasswordPage />} />,
  <Route key="password-reset" path="/password-reset" element={<PasswordResetPage />} />,
  <Route key="select-subscriber" path="/select-subscriber" element={<SubscriberSelectPage />} />,
  <Route key="select-company" path="/select-company" element={<CompanySelectPage />} />,
];
