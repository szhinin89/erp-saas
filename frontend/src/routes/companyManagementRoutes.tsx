import { Route } from 'react-router-dom';
import { lazyNamedPage } from './lazyPage';

const CompanyManagementHubPage = lazyNamedPage(
  () => import('../modules/company-management/pages/CompanyManagementHubPage'),
  'CompanyManagementHubPage',
);
const CompanyManagementFormPage = lazyNamedPage<{ mode: 'create' | 'edit' }>(
  () => import('../modules/company-management/pages/CompanyManagementFormPage'),
  'CompanyManagementFormPage',
);
const SaasOverviewPage = lazyNamedPage(() => import('../pages/saas/SaasOverviewPage'), 'SaasOverviewPage');
const SaasBillingPage = lazyNamedPage(() => import('../pages/saas/SaasBillingPage'), 'SaasBillingPage');

export const companyManagementRoutes = [
  <Route key="saas-overview" path="/saas/overview" element={<SaasOverviewPage />} />,
  <Route key="saas-billing" path="/saas/billing" element={<SaasBillingPage />} />,
  <Route key="saas-companies" path="/saas/companies" element={<CompanyManagementHubPage />} />,
  <Route
    key="saas-companies-new"
    path="/saas/companies/new"
    element={<CompanyManagementFormPage mode="create" />}
  />,
  <Route
    key="saas-companies-edit"
    path="/saas/companies/:id/edit"
    element={<CompanyManagementFormPage mode="edit" />}
  />,
];
