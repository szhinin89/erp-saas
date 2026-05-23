import { BrowserRouter, Routes, Route } from 'react-router-dom';
import './pages/legacy-pages.css';
import { ProtectedRoute } from './components/ProtectedRoute';
import { HomeRedirect } from './components/HomeRedirect';
import { AppLayout } from './components/AppLayout';
import { useDeployment } from './deployment/DeploymentContext';
import { ConfigProvider } from './modules/config';
import {
  publicRoutes,
  adminRoutes,
  mainRoutes,
  catalogRoutes,
  companyManagementRoutes,
  accessRoutes,
} from './routes';
import { superAdminShellRoutes } from './routes/superAdminShellRoutes';
import { CompaniesLegacyRedirect } from './routes/CompaniesLegacyRedirect';
import { SessionBootstrap } from './components/SessionBootstrap';

function AppRoutes() {
  const { superAdminPanelEnabled } = useDeployment();

  return (
    <BrowserRouter>
      <Routes>
        {publicRoutes}

        <Route element={<ProtectedRoute />}>
          {superAdminPanelEnabled ? superAdminShellRoutes() : null}
          <Route path="/companies/*" element={<CompaniesLegacyRedirect />} />
          <Route element={<AppLayout />}>
            <Route index element={<HomeRedirect />} />
            {adminRoutes(superAdminPanelEnabled)}
            {mainRoutes}
            {catalogRoutes}
            {companyManagementRoutes}
            {accessRoutes}
          </Route>
        </Route>

        <Route path="*" element={<HomeRedirect />} />
      </Routes>
    </BrowserRouter>
  );
}

export default function App() {
  return (
    <SessionBootstrap>
      <ConfigProvider>
        <AppRoutes />
      </ConfigProvider>
    </SessionBootstrap>
  );
}
