import { Route } from 'react-router-dom';
import {
  CompanyManagementHubPage,
  CompanyManagementFormPage,
} from '../modules/company-management';

export const companyManagementRoutes = [
  <Route key="saas-companies" path="/saas/companies" element={<CompanyManagementHubPage />} />,
  <Route key="saas-companies-new" path="/saas/companies/new" element={<CompanyManagementFormPage mode="create" />} />,
  <Route key="saas-companies-edit" path="/saas/companies/:id/edit" element={<CompanyManagementFormPage mode="edit" />} />,
];
