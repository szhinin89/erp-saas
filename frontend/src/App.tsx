import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ProtectedRoute } from './components/ProtectedRoute';
import { AppLayout } from './components/AppLayout';
import { useDeployment } from './deployment/DeploymentContext';
import { ConfigProvider } from './modules/config';
import {
  publicRoutes,
  adminRoutes,
  mainRoutes,
  catalogRoutes,
  companiesRoutes,
  companyManagementRoutes,
  accessRoutes,
} from './routes';
import { superAdminShellRoutes } from './routes/superAdminShellRoutes';

function AppRoutes() {
  const { superAdminPanelEnabled } = useDeployment();

  return (
    <BrowserRouter>
      <Routes>
        {publicRoutes}

        <Route element={<ProtectedRoute />}>
          {superAdminPanelEnabled ? superAdminShellRoutes() : null}
          <Route element={<AppLayout />}>
            {adminRoutes(superAdminPanelEnabled)}
            {mainRoutes}
            {catalogRoutes}
            {companiesRoutes}
            {companyManagementRoutes}
            {accessRoutes}
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default function App() {
  return (
    <ConfigProvider>
      <AppRoutes />
    </ConfigProvider>
  );
}
