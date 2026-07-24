import { Route } from 'react-router-dom';
import { lazyNamedPage } from './lazyPage';
import { SetupPage } from '../pages/SetupPage';

const LoginPage = lazyNamedPage(() => import('../modules/auth/pages/LoginPage'), 'LoginPage');
const ForgotPasswordPage = lazyNamedPage(
  () => import('../modules/auth/pages/ForgotPasswordPage'),
  'ForgotPasswordPage',
);
const ResetPasswordPage = lazyNamedPage(
  () => import('../modules/auth/pages/ResetPasswordPage'),
  'ResetPasswordPage',
);
const CompanySelectPage = lazyNamedPage(
  () => import('../modules/auth/pages/CompanySelectPage'),
  'CompanySelectPage',
);
const CompletePasswordResetPage = lazyNamedPage(
  () => import('../modules/auth/pages/CompletePasswordResetPage'),
  'CompletePasswordResetPage',
);

export const publicRoutes = [
  <Route key="login" path="/login" element={<LoginPage />} />,
  <Route key="forgot-password" path="/forgot-password" element={<ForgotPasswordPage />} />,
  <Route key="reset-password" path="/reset-password" element={<ResetPasswordPage />} />,
  <Route key="select-company" path="/select-company" element={<CompanySelectPage />} />,
  <Route key="complete-password-reset" path="/complete-password-reset" element={<CompletePasswordResetPage />} />,
  // First-run ERP setup
  <Route key="setup" path="/setup" element={<SetupPage />} />,
];
