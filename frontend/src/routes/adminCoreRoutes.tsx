import { Route } from "react-router-dom";
import { lazyNamedPage } from "./lazyPage";

const AdminCoreDashboardPage = lazyNamedPage(
  () => import("../modules/admin-core/pages/AdminCoreDashboardPage"),
  "AdminCoreDashboardPage",
);
const AdminCoreCompanyCreatePage = lazyNamedPage(
  () => import("../modules/admin-core/pages/AdminCoreCompanyCreatePage"),
  "AdminCoreCompanyCreatePage",
);

/** Rutas protegidas de AdminGlobalCore — montadas dentro de AdminCoreProtectedRoute/AdminCoreLayout, nunca dentro de AppLayout. */
export const adminCoreRoutes = [
  <Route
    key="admin-core-dashboard"
    path="/admin-core/dashboard"
    element={<AdminCoreDashboardPage />}
  />,
  <Route
    key="admin-core-companies-new"
    path="/admin-core/companies/new"
    element={<AdminCoreCompanyCreatePage />}
  />,
];
