import { publicRoutes } from './publicRoutes';
import { adminRoutes } from './adminRoutes';
import { catalogRoutes } from './catalogRoutes';
import { companiesRoutes } from './companiesRoutes';
import { accessRoutes } from './accessRoutes';
import { mainRoutes } from './mainRoutes';

export interface AppRoutesConfig {
  superAdminPanelEnabled: boolean;
}

/**
 * Compone todas las rutas de la aplicación.
 * 
 * Ventajas sobre el enfoque monolítico en App.tsx:
 * 1. Cada módulo declara sus propias rutas
 * 2. Las rutas se pueden agregar/remover por configuración
 * 3. Fácil de testear (cada ruta file es independiente)
 * 4. Escalable cuando se agregan nuevos módulos
 * 
 * Estructura de rutas:
 * - publicRoutes: Login, TenantSelect (sin autenticación)
 * - adminRoutes: SuperAdmin panel (configurado)
 * - mainRoutes: Dashboard, Productos, Ventas, Contabilidad, etc.
 * - catalogRoutes: Brands, Product Types, Units, etc.
 * - companiesRoutes: Gestión de empresas (SaaS)
 * - accessRoutes: Seguridad, Acceso, Perfiles
 * 
 * @param config Configuración de la app (superAdminPanelEnabled, etc.)
 * @returns Array de Route components
 */
export function getAppRoutes(config: AppRoutesConfig) {
  return [
    ...publicRoutes,
    ...adminRoutes(config.superAdminPanelEnabled),
    ...mainRoutes,
    ...catalogRoutes,
    ...companiesRoutes,
    ...accessRoutes,
  ];
}

// Exportar también las rutas individuales para reuso
export { publicRoutes } from './publicRoutes';
export { adminRoutes } from './adminRoutes';
export { catalogRoutes } from './catalogRoutes';
export { companiesRoutes } from './companiesRoutes';
export { accessRoutes } from './accessRoutes';
export { mainRoutes } from './mainRoutes';
