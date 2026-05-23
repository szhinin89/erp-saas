import { BrowserRouter, Routes, Route } from 'react-router-dom';
import './pages/legacy-pages.css';
import { ProtectedRoute } from './components/ProtectedRoute';
import { HomeRedirect } from './components/HomeRedirect';
import { AppLayout } from './components/AppLayout';
import { useDeployment } from './deployment/DeploymentContext';
import { ConfigProvider } from './modules/config';
import {
  publicRoutes,
  mainRoutes,
  catalogRoutes,
  companyManagementRoutes,
  accessRoutes,
} from './routes';
import { platformShellRoutes, platformBookmarkRedirectRoutes } from './routes/platformRoutes';
import { SessionBootstrap } from './components/SessionBootstrap';

function AppRoutes() {
  const { superAdminPanelEnabled } = useDeployment();

  return (
    <BrowserRouter>
      <Routes>
        {publicRoutes}

        <Route element={<ProtectedRoute />}>
          {superAdminPanelEnabled ? platformShellRoutes() : null}
          {superAdminPanelEnabled ? platformBookmarkRedirectRoutes() : null}
          <Route element={<AppLayout />}>
            <Route index element={<HomeRedirect />} />
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
