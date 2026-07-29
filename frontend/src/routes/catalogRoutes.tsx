import { Route, Navigate } from "react-router-dom";
import { lazyNamedPage } from "./lazyPage";

const GeographyPage = lazyNamedPage(
  () => import("../modules/settings/geography/pages/GeographyPage"),
  "GeographyPage",
);
const ActivityPage = lazyNamedPage(
  () => import("../modules/admin/activity/pages/ActivityPage"),
  "ActivityPage",
);
const AdminUserSessionsPage = lazyNamedPage(
  () => import("../modules/admin/access-sessions/pages/AdminUserSessionsPage"),
  "AdminUserSessionsPage",
);
const BodegasPage = lazyNamedPage(
  () => import("../modules/inventory/warehouses/pages/WarehousesPage"),
  "BodegasPage",
);
const CarriersPage = lazyNamedPage(
  () => import("../modules/logistica/transportistas/pages/CarriersPage"),
  "CarriersPage",
);
const PriceListsPage = lazyNamedPage(
  () => import("../modules/pricing/pages/PriceListsPage"),
  "PriceListsPage",
);
const CreditTermsPage = lazyNamedPage(
  () => import("../modules/finance/pages/CreditTermsPage"),
  "CreditTermsPage",
);
const PaymentTermsPage = lazyNamedPage(
  () => import("../modules/masterData/pages/PaymentTermsPage"),
  "PaymentTermsPage",
);
const PurchasesPage = lazyNamedPage(
  () => import("../modules/purchases/pages/PurchasesPage"),
  "PurchasesPage",
);
const PurchaseReceptionPage = lazyNamedPage(
  () => import("../modules/purchases/pages/PurchaseReceptionPage"),
  "PurchaseReceptionPage",
);
const SalesPage = lazyNamedPage(
  () => import("../modules/sales/pages/SalesPage"),
  "SalesPage",
);
const PaymentMethodsPage = lazyNamedPage(
  () => import("../modules/sales/pages/PaymentMethodsPage"),
  "PaymentMethodsPage",
);
const CajaPage = lazyNamedPage(
  () => import("../modules/caja/pages/CajaPage"),
  "CajaPage",
);
const CashRegistersPage = lazyNamedPage(
  () => import("../modules/cashRegisters/pages/CashRegistersPage"),
  "CashRegistersPage",
);
const BrandsPage = lazyNamedPage(
  () => import("../modules/items/catalog/pages/BrandsPage"),
  "BrandsPage",
);
const AttributeGroupsPage = lazyNamedPage(
  () => import("../modules/items/catalog/pages/AttributeGroupsPage"),
  "AttributeGroupsPage",
);
const AttributeDefinitionsPage = lazyNamedPage(
  () => import("../modules/items/catalog/pages/AttributeDefinitionsPage"),
  "AttributeDefinitionsPage",
);
const TreeEditorPage = lazyNamedPage(
  () => import("../modules/items/catalog/wizard/TreeEditor"),
  "TreeEditorPage",
);
export const catalogRoutes = [
  // -- Inventory / Warehouses ---------------------------------------------
  <Route
    key="inventory-warehouses"
    path="/inventory/warehouses"
    element={<BodegasPage />}
  />,
  <Route
    key="inventario-bodegas"
    path="/inventario/bodegas"
    element={<Navigate to="/inventory/warehouses" replace />}
  />,
  <Route
    key="logistica-bodegas"
    path="/logistica/bodegas"
    element={<Navigate to="/inventory/warehouses" replace />}
  />,

  // -- Logistics / Transportistas -----------------------------------------
  <Route
    key="logistics-carriers"
    path="/logistics/carriers"
    element={<CarriersPage />}
  />,
  <Route
    key="logistica-transportistas"
    path="/logistica/transportistas"
    element={<Navigate to="/logistics/carriers" replace />}
  />,

  // -- Settings -----------------------------------------------------------
  <Route
    key="settings-geography"
    path="/settings/geography"
    element={<GeographyPage />}
  />,
  <Route
    key="geo-legacy"
    path="/configuracion/geografia"
    element={<Navigate to="/settings/geography" replace />}
  />,

  // -- Admin / Activity ---------------------------------------------------
  <Route
    key="admin-activity"
    path="/admin/activity"
    element={<ActivityPage />}
  />,
  <Route
    key="actividad-legacy"
    path="/actividad"
    element={<Navigate to="/admin/activity" replace />}
  />,
  <Route
    key="admin-access-sessions"
    path="/admin/access/sessions"
    element={<AdminUserSessionsPage />}
  />,

  // -- Pricing ------------------------------------------------------------
  <Route key="pricing" path="/pricing" element={<PriceListsPage />} />,

  // -- Purchases -----------------------------------------------------------
  <Route key="purchases" path="/purchases" element={<PurchasesPage />} />,
  <Route
    key="purchase-reception"
    path="/purchases/reception"
    element={<PurchaseReceptionPage />}
  />,

  // -- Sales ---------------------------------------------------------------
  <Route key="sales" path="/sales" element={<SalesPage />} />,
  <Route
    key="sales-payment-methods"
    path="/sales/payment-methods"
    element={<PaymentMethodsPage />}
  />,

  // -- Caja (Cash Management) ---------------------------------------------
  <Route key="cash" path="/cash" element={<CajaPage />} />,
  <Route
    key="cash-registers"
    path="/cash/registers"
    element={<CashRegistersPage />}
  />,

  // -- Finance ------------------------------------------------------------
  <Route
    key="finance-credit-terms"
    path="/finance/credit-terms"
    element={<CreditTermsPage />}
  />,

  // -- MasterData / Payment Terms -----------------------------------------
  <Route
    key="master-payment-terms"
    path="/master/payment-terms"
    element={<PaymentTermsPage />}
  />,

  // -- Catalog ------------------------------------------------------------
  <Route
    key="catalog-brands"
    path="/catalog/brands"
    element={<BrandsPage />}
  />,
  <Route
    key="catalog-attribute-groups"
    path="/catalog/attribute-groups"
    element={<AttributeGroupsPage />}
  />,
  <Route
    key="catalog-attribute-definitions"
    path="/catalog/attribute-definitions"
    element={<AttributeDefinitionsPage />}
  />,
  <Route
    key="catalog-tree"
    path="/catalog/tree"
    element={<TreeEditorPage />}
  />,
];
