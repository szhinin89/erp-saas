import { Route } from 'react-router-dom';
import {
  CompanyManagementHubPage,
  CompanyManagementFormPage,
} from '../modules/company-management';
import { SaasOverviewPage } from '../pages/saas/SaasOverviewPage';
import { SaasBillingPage } from '../pages/saas/SaasBillingPage';

export const companyManagementRoutes = [
  <Route key="saas-overview" path="/saas/overview" element={<SaasOverviewPage />} />,
  <Route key="saas-billing" path="/saas/billing" element={<SaasBillingPage />} />,
  <Route key="saas-companies" path="/saas/companies" element={<CompanyManagementHubPage />} />,
  <Route key="saas-companies-new" path="/saas/companies/new" element={<CompanyManagementFormPage mode="create" />} />,
  <Route key="saas-companies-edit" path="/saas/companies/:id/edit" element={<CompanyManagementFormPage mode="edit" />} />,
];
