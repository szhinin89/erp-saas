import { Route } from 'react-router-dom';
import { lazyNamedPage } from './lazyPage';
import { SubscriptionSuspendedPage } from '../pages/SubscriptionSuspendedPage';

const LoginPage = lazyNamedPage(() => import('../modules/auth/pages/LoginPage'), 'LoginPage');
const ForgotPasswordPage = lazyNamedPage(
  () => import('../modules/auth/pages/ForgotPasswordPage'),
  'ForgotPasswordPage',
);
const ResetPasswordPage = lazyNamedPage(
  () => import('../modules/auth/pages/ResetPasswordPage'),
  'ResetPasswordPage',
);
const PasswordResetPage = lazyNamedPage(
  () => import('../modules/auth/pages/PasswordResetPage'),
  'PasswordResetPage',
);
const SubscriberSelectPage = lazyNamedPage(
  () => import('../modules/auth/pages/SubscriberSelectPage'),
  'SubscriberSelectPage',
);
const CompanySelectPage = lazyNamedPage(
  () => import('../modules/auth/pages/CompanySelectPage'),
  'CompanySelectPage',
);

export const publicRoutes = [
  <Route key="login" path="/login" element={<LoginPage />} />,
  <Route key="forgot-password" path="/forgot-password" element={<ForgotPasswordPage />} />,
  <Route key="reset-password" path="/reset-password" element={<ResetPasswordPage />} />,
  <Route key="password-reset" path="/password-reset" element={<PasswordResetPage />} />,
  <Route key="select-subscriber" path="/select-subscriber" element={<SubscriberSelectPage />} />,
  <Route key="select-company" path="/select-company" element={<CompanySelectPage />} />,
  <Route key="subscription-suspended" path="/subscription-suspended" element={<SubscriptionSuspendedPage />} />,
];
