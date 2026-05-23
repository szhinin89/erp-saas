import type { NavItemSiblingOrderLevel } from './platformService';
import {
  platformService,
  type AdminNavigationMenu,
  type CreateNavItemBody,
  type UpdateNavItemBody,
} from './platformService';

/** Fachada del menú maestro global (`ui_nav_*`). */
export const menuService = {
  getMenuTree: (): Promise<AdminNavigationMenu> => platformService.getNavigationMenu(),

  createMenuItem: (body: CreateNavItemBody): Promise<string> => platformService.createNavigationMenuItem(body),

  reorderGroups: (orderedGroupIds: string[]) => platformService.reorderNavigationGroups(orderedGroupIds),

  reorderMenu: (levels: NavItemSiblingOrderLevel[]) => platformService.reorderNavigationItemLevels(levels),

  updateMenuItem: (itemId: string, patch: UpdateNavItemBody): Promise<void> =>
    platformService.updateNavigationMenuItem(itemId, patch),

  deleteMenuItem: (itemId: string): Promise<void> => platformService.deleteNavigationMenuItem(itemId),
};
