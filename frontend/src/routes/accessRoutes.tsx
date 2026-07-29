import { Route, Navigate } from "react-router-dom";
import { lazyNamedPage } from "./lazyPage";

const SecuritySettingsPage = lazyNamedPage(
  () => import("../modules/security/pages/SecuritySettingsPage"),
  "SecuritySettingsPage",
);
const ProfilesPage = lazyNamedPage(
  () => import("../modules/access/pages/ProfilesPage"),
  "ProfilesPage",
);
const UsersPage = lazyNamedPage(
  () => import("../modules/access/users/pages/UsersPage"),
  "UsersPage",
);
const UserConfigPage = lazyNamedPage(
  () => import("../modules/access/users/pages/UserConfigPage"),
  "UserConfigPage",
);

export const accessRoutes = [
  <Route
    key="admin-security"
    path="/admin/security"
    element={<SecuritySettingsPage />}
  />,
  <Route key="access-users" path="/access/users" element={<UsersPage />} />,
  <Route
    key="access-users-new"
    path="/access/users/new"
    element={<UserConfigPage />}
  />,
  <Route
    key="access-users-detail"
    path="/access/users/:userId"
    element={<UserConfigPage />}
  />,
  <Route
    key="admin-users-legacy"
    path="/admin/users"
    element={<Navigate to="/access/users" replace />}
  />,
  <Route key="admin-roles" path="/admin/roles" element={<ProfilesPage />} />,
  <Route
    key="security-legacy"
    path="/security"
    element={<Navigate to="/admin/security" replace />}
  />,
];
