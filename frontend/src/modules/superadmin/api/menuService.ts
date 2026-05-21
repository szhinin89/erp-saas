import type { NavItemSiblingOrderLevel } from './superAdminService';
import {
  superAdminService,
  type AdminNavigationMenu,
  type CreateNavItemBody,
  type UpdateNavItemBody,
} from './superAdminService';

/** Fachada del menú maestro global (`ui_nav_*`). */
export const menuService = {
  getMenuTree: (): Promise<AdminNavigationMenu> => superAdminService.getNavigationMenu(),

  createMenuItem: (body: CreateNavItemBody): Promise<string> => superAdminService.createNavigationMenuItem(body),

  reorderGroups: (orderedGroupIds: string[]) => superAdminService.reorderNavigationGroups(orderedGroupIds),

  reorderMenu: (levels: NavItemSiblingOrderLevel[]) => superAdminService.reorderNavigationItemLevels(levels),

  updateMenuItem: (itemId: string, patch: UpdateNavItemBody): Promise<void> =>
    superAdminService.updateNavigationMenuItem(itemId, patch),

  deleteMenuItem: (itemId: string): Promise<void> => superAdminService.deleteNavigationMenuItem(itemId),
};
