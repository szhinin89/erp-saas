import { Route } from "react-router-dom";
import { lazyNamedPage } from "./lazyPage";

const EmployeesPage = lazyNamedPage(
  () => import("../modules/employees/pages/EmployeesPage"),
  "EmployeesPage",
);
const EmployeeCatalogsPage = lazyNamedPage(
  () => import("../modules/employees/pages/EmployeeCatalogsPage"),
  "EmployeeCatalogsPage",
);
const EmployeeDetailPage = lazyNamedPage(
  () => import("../modules/employees/pages/EmployeeDetailPage"),
  "EmployeeDetailPage",
);

/** EMP-E7. La visibilidad la decide el menú del servidor (RouteAccessGuard); `:id` la hereda. */
export const employeesRoutes = [
  <Route key="employees" path="/employees" element={<EmployeesPage />} />,
  <Route key="employees-catalogs" path="/employees/catalogs" element={<EmployeeCatalogsPage />} />,
  <Route key="employees-detail" path="/employees/:id" element={<EmployeeDetailPage />} />,
];
