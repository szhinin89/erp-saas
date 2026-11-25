import { Routes, Route, useNavigate } from "react-router-dom";
import { useEffect } from "react";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { AdminCoreProtectedRoute } from "./components/AdminCoreProtectedRoute";
import { HomeRedirect } from "./components/HomeRedirect";
import { AppLayout } from "./components/AppLayout";
import { AdminCoreLayout } from "./modules/admin-core/components/AdminCoreLayout";
import {
  publicRoutes,
  mainRoutes,
  catalogRoutes,
  accessRoutes,
  employeesRoutes,
  adminCoreRoutes,
} from "./routes";
import { registerGlobalNavigator } from "./lib/navigation/globalNavigator";

/** Registers the React Router navigate function so axios interceptors can do SPA navigation. */
function GlobalNavigatorRegistrar() {
  const navigate = useNavigate();
  useEffect(() => {
    registerGlobalNavigator((path) => navigate(path));
  }, [navigate]);
  return null;
}

/**
 * Árbol de rutas de la aplicación (sin cambios respecto al anterior App.tsx). Se monta bajo un
 * router de datos mediante una ruta comodín (`path="*"`, ver App.tsx): migración incremental que
 * habilita `useBlocker` (BP-NAV-GUARD-01) sin redefinir rutas, guards ni layouts.
 */
export function AppRoutes() {
  return (
    <>
      <GlobalNavigatorRegistrar />
      <Routes>
        {publicRoutes}

        <Route element={<ProtectedRoute />}>
          <Route element={<AppLayout />}>
            <Route index element={<HomeRedirect />} />
            {mainRoutes}
            {catalogRoutes}
            {accessRoutes}
            {employeesRoutes}
          </Route>
        </Route>

        {/* AdminGlobalCore — shell separado, nunca anidado dentro de AppLayout/ProtectedRoute */}
        <Route element={<AdminCoreProtectedRoute />}>
          <Route element={<AdminCoreLayout />}>{adminCoreRoutes}</Route>
        </Route>

        <Route path="*" element={<HomeRedirect />} />
      </Routes>
    </>
  );
}
