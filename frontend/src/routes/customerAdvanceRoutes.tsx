import { Route } from "react-router-dom";
import { lazyNamedPage } from "./lazyPage";

const CustomerAdvanceListPage = lazyNamedPage(
  () => import("../modules/finance/pages/CustomerAdvanceListPage"),
  "CustomerAdvanceListPage",
);
const CustomerAdvanceDetailPage = lazyNamedPage(
  () => import("../modules/finance/pages/CustomerAdvanceDetailPage"),
  "CustomerAdvanceDetailPage",
);

/** Anticipos de clientes (Finance): se incluyen en `catalogRoutes`, junto a Cuentas por cobrar. */
export const customerAdvanceRoutes = [
  <Route
    key="finance-customer-advances"
    path="/finance/customer-advances"
    element={<CustomerAdvanceListPage />}
  />,
  <Route
    key="finance-customer-advances-detail"
    path="/finance/customer-advances/:id"
    element={<CustomerAdvanceDetailPage />}
  />,
];
