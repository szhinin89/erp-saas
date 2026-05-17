import { Route, Navigate } from 'react-router-dom';
import { DashboardPage } from '../pages/DashboardPage';
import { ProductsPage } from '../pages/ProductsPage';
import { AccountingPage } from '../pages/AccountingPage';
import { CustomersPage } from '../pages/CustomersPage';
import { VentasFacturasPage } from '../pages/VentasFacturasPage';
import { CreateInvoicePage } from '../modules/ventas/pages/CreateInvoicePage';
import { BranchesPage } from '../pages/BranchesPage';
import { ModulePlaceholderPage } from '../pages/ModulePlaceholderPage';
import { SalesReportPage } from '../pages/SalesReportPage';
import { SuppliersPage } from '../modules/compras/suppliers/pages/SuppliersPage';
import { CompanyConfigPage } from '../modules/configuracion/empresa/pages/CompanyConfigPage';
import { SriConfigPage } from '../modules/configuracion/sri/pages/SriConfigPage';
import { BillingSettingsPage } from '../modules/configuracion/facturacion/pages/BillingSettingsPage';

/**
 * Rutas principales de aplicación.
 * Todas están bajo <ProtectedRoute /> y <AppLayout />.
 * 
 * Incluye:
 * - Dashboard
 * - Productos (con redirecciones legacy)
 * - Ventas (Clientes)
 * - Contabilidad
 * - Compras y RRHH (placeholders)
 * - Sucursales (SaaS)
 */
export const mainRoutes = [
  <Route key="dashboard" path="/dashboard" element={<DashboardPage />} />,
  
  // Productos
  <Route key="products" path="/products" element={<ProductsPage />} />,
  <Route key="dashboard-products" path="/dashboard/products" element={<Navigate to="/products" replace />} />,
  <Route key="inventario-products" path="/inventario/products" element={<Navigate to="/products" replace />} />,
  <Route key="catalog-products" path="/catalog/products" element={<Navigate to="/products" replace />} />,
  <Route key="product" path="/product" element={<Navigate to="/products" replace />} />,
  
  // Ventas
  <Route key="customers" path="/ventas/customers" element={<CustomersPage />} />,
  <Route key="ventas-clientes-es" path="/ventas/clientes" element={<Navigate to="/ventas/customers" replace />} />,
  <Route key="ventas-facturas" path="/ventas/facturas" element={<VentasFacturasPage />} />,
  <Route key="ventas-facturas-nueva" path="/ventas/facturas/nueva" element={<CreateInvoicePage />} />,
  <Route key="catalog-customers" path="/catalog/customers" element={<Navigate to="/ventas/customers" replace />} />,
  <Route key="inventario-customers" path="/inventario/customers" element={<Navigate to="/ventas/customers" replace />} />,
  
  // Contabilidad
  <Route key="accounting" path="/accounting" element={<AccountingPage />} />,
  <Route key="contabilidad" path="/contabilidad" element={<Navigate to="/accounting" replace />} />,
  <Route key="contabilidad-config" path="/contabilidad/configuracion" element={<Navigate to="/accounting" replace />} />,

  // Ventas raíz (catálogo API usa /ventas)
  <Route key="ventas-root" path="/ventas" element={<Navigate to="/ventas/facturas" replace />} />,

  // Reportes
  <Route key="sales-report" path="/reportes/ventas" element={<SalesReportPage />} />,

  // Compras
  <Route key="proveedores" path="/compras/proveedores" element={<SuppliersPage />} />,
  <Route key="compras" path="/compras" element={<ModulePlaceholderPage variant="purchases" />} />,
  <Route key="rrhh" path="/rrhh" element={<ModulePlaceholderPage variant="hr" />} />,

  // Configuración
  <Route key="config-empresa"      path="/configuracion/empresa"      element={<CompanyConfigPage />} />,
  <Route key="config-sri"          path="/configuracion/sri"           element={<SriConfigPage />} />,
  <Route key="config-facturacion"  path="/configuracion/facturacion"   element={<BillingSettingsPage />} />,

  // SaaS
  <Route key="branches" path="/saas/branches" element={<BranchesPage />} />,
  <Route key="config-sucursales" path="/configuracion/sucursales" element={<Navigate to="/saas/branches" replace />} />,
];