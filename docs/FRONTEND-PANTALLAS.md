# Catálogo de Formularios (Frontend)

> **Ubicación canónica:** `docs/FRONTEND-PANTALLAS.md`. Las rutas `src/...` son relativas al directorio **`frontend/`**.

Este archivo lista **todas las pantallas/formularios** disponibles en el frontend para tener un inventario claro.

Fuente de verdad actual:
- **Rutas**: `src/App.tsx`
- **Menú / módulos** (y permisos): `src/nav/navConfig.ts`

> Cuando se cree una pantalla nueva, agregarla en:
> 1) `src/App.tsx` (ruta)
> 2) `src/nav/navConfig.ts` (si debe aparecer en menú / catálogo)
> 3) **`docs/FRONTEND-PANTALLAS.md`** (para inventario rápido)

## Auth / Sesión
- **Login**: `/login` → `src/pages/LoginPage.tsx`
- **Reset contraseña**: `/password-reset` → `src/pages/PasswordResetPage.tsx`
- **Selección de empresa**: `/select-tenant` → `src/pages/TenantSelectPage.tsx`

## Inicio
- **Dashboard (empresa)**: `/dashboard` → `src/pages/DashboardPage.tsx`

## Catálogo
- **Productos**: `/products` → `src/pages/ProductsPage.tsx`  
  - Permiso: `catalog.products.view` (ver), `catalog.products.create` (crear)
- **Marcas**: `/catalog/brands` → `src/modules/catalog/pages/CatalogPages.tsx`  
  - Permiso: `catalog.brands.view`
- **Tipos de producto**: `/catalog/product-types` → `src/modules/catalog/pages/CatalogPages.tsx`  
  - Permiso: `catalog.productTypes.view`
- **Unidades**: `/catalog/units` → `src/modules/catalog/pages/CatalogPages.tsx`  
  - Permiso: `catalog.units.view`
- **Impuestos**: `/catalog/tax-rates` → `src/modules/catalog/pages/CatalogPages.tsx`  
  - Permiso: `catalog.taxRates.view`
- **Aranceles**: `/catalog/tariffs` → `src/modules/catalog/pages/CatalogPages.tsx`  
  - Permiso: `catalog.tariffs.view`
- **Categorización**: `/catalog/structure` → `src/modules/catalog/pages/CatalogPages.tsx`  
  - Permiso: `catalog.categories.view`

## Contabilidad
- **Contabilidad**: `/accounting` → `src/pages/AccountingPage.tsx`

## Accesos
- **Accesos del tenant**: `/access` → `src/pages/TenantAccessPage.tsx`  
  - Roles: `Admin`, `SuperAdmin`
- **Perfiles**: `/profiles` → `src/pages/ProfilesPage.tsx`  
  - Roles: `Admin`, `SuperAdmin`
- **Sucursales (SaaS)**: `/saas/branches` → `src/pages/BranchesPage.tsx`  
  - Roles: `Admin`, `SuperAdmin`  
  - Permiso: `saas.branches.view`

## Seguridad (SuperAdmin)
- **Configuración de seguridad**: `/security` → `src/pages/SecuritySettingsPage.tsx`  
  - Rol: `SuperAdmin`

## SaaS (SuperAdmin)
- **Empresas**: `/companies` → `src/pages/CompaniesPage.tsx`  
  - Rol: `SuperAdmin`  
  - Pestañas: **Datos** (empresa + admin), **Plan y módulos** (`TenantSubscriptionEditor`, catálogo por menú `TenantSubscriptionMenuCatalog`), **Plan ↔ menú** (`CompaniesPlanMenuAssignment`), **Auditoría**.  
  - Contratos API y flujo vigente (plan comercial + menú): `docs/COMPANIES-PLAN-MENU-ADMIN.md`
- **Panel Global**: `/superadmin` → `src/pages/SuperAdminPanelPage.tsx`  
  - Rol: `SuperAdmin`
- **Menú de navegación (SuperAdmin)**: `/superadmin/navigation-menu` → `src/pages/SuperAdminNavMenuPage.tsx`  
  - Rol: `SuperAdmin` (árbol alineado con el catálogo SaaS en empresas)
- **Formularios (SuperAdmin)**: `/superadmin/forms` → `src/pages/SuperAdminFormsPage.tsx`  
  - Rol: `SuperAdmin`

