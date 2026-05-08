import { Route } from 'react-router-dom';
import { CompaniesPage } from '../modules/companies';

/**
 * Rutas del módulo Companies (SaaS).
 * Todas están bajo <ProtectedRoute /> y <AppLayout />.
 * 
 * Paths:
 * - /companies (principal)
 * - /saas/branches (relacionado a empresas)
 * 
 * Nota: BranchesPage se mantiene en esta ruta por ahora,
 * pero podría moverse a un módulo 'branches' separado en el futuro.
 */
export const companiesRoutes = [
  <Route key="companies" path="/companies" element={<CompaniesPage />} />,
];
