import { Route } from "react-router-dom";
import { lazyNamedPage } from "./lazyPage";

const CompanyManagementHubPage = lazyNamedPage(
  () => import("../modules/company-management/pages/CompanyManagementHubPage"),
  "CompanyManagementHubPage",
);
const CompanyManagementFormPage = lazyNamedPage<{ mode: "create" | "edit" }>(
  () => import("../modules/company-management/pages/CompanyManagementFormPage"),
  "CompanyManagementFormPage",
);

export const companyManagementRoutes = [
  <Route
    key="erp-companies"
    path="/companies"
    element={<CompanyManagementHubPage />}
  />,
  <Route
    key="erp-companies-new"
    path="/companies/new"
    element={<CompanyManagementFormPage mode="create" />}
  />,
  <Route
    key="erp-companies-edit"
    path="/companies/:id/edit"
    element={<CompanyManagementFormPage mode="edit" />}
  />,
];
