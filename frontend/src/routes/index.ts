import { publicRoutes } from './publicRoutes';
import { catalogRoutes } from './catalogRoutes';
import { companyManagementRoutes } from './companyManagementRoutes';
import { accessRoutes } from './accessRoutes';
import { mainRoutes } from './mainRoutes';

export interface AppRoutesConfig {
  platformPanelEnabled: boolean;
}

/**
 * Compone rutas ERP runtime + SaaS tenant (sin platform shell — ver `platformRoutes.tsx`).
 */
export function getAppRoutes(config: AppRoutesConfig) {
  void config;
  return [
    ...publicRoutes,
    ...mainRoutes,
    ...catalogRoutes,
    ...companyManagementRoutes,
    ...accessRoutes,
  ];
}

export { publicRoutes } from './publicRoutes';
export { catalogRoutes } from './catalogRoutes';
export { companyManagementRoutes } from './companyManagementRoutes';
export { accessRoutes } from './accessRoutes';
export { mainRoutes } from './mainRoutes';
export { platformShellRoutes, platformBookmarkRedirectRoutes } from './platformRoutes';
