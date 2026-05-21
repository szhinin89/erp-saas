import { Route } from 'react-router-dom';
import { lazyNamedPage } from './lazyPage';

const CompaniesPage = lazyNamedPage(() => import('../modules/companies/pages/CompaniesPage'), 'CompaniesPage');

export const companiesRoutes = [
  <Route key="companies" path="/companies" element={<CompaniesPage />} />,
];
