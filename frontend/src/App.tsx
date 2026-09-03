import { BrowserRouter, Routes, Route, useNavigate } from "react-router-dom";
import { useEffect } from "react";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { AdminCoreProtectedRoute } from "./components/AdminCoreProtectedRoute";
import { HomeRedirect } from "./components/HomeRedirect";
import { AppLayout } from "./components/AppLayout";
import { AdminCoreLayout } from "./modules/admin-core/components/AdminCoreLayout";
import { ConfigProvider } from "./modules/config";
import {
  publicRoutes,
  mainRoutes,
  catalogRoutes,
  companyManagementRoutes,
  accessRoutes,
  adminCoreRoutes,
} from "./routes";
import { SessionBootstrap } from "./components/SessionBootstrap";
import { registerGlobalNavigator } from "./lib/navigation/globalNavigator";

/** Registers the React Router navigate function so axios interceptors can do SPA navigation. */
function GlobalNavigatorRegistrar() {
  const navigate = useNavigate();
  useEffect(() => {
    registerGlobalNavigator((path) => navigate(path));
  }, [navigate]);
  return null;
}

function AppRoutes() {
  return (
    <BrowserRouter>
      <GlobalNavigatorRegistrar />
      <Routes>
        {publicRoutes}

        <Route element={<ProtectedRoute />}>
          <Route element={<AppLayout />}>
            <Route index element={<HomeRedirect />} />
            {mainRoutes}
            {catalogRoutes}
            {companyManagementRoutes}
            {accessRoutes}
          </Route>
        </Route>

        {/* AdminGlobalCore — shell separado, nunca anidado dentro de AppLayout/ProtectedRoute */}
        <Route element={<AdminCoreProtectedRoute />}>
          <Route element={<AdminCoreLayout />}>{adminCoreRoutes}</Route>
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
