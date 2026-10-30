import { createContext, useContext } from "react";
import type { MainMenuGroup } from "./useAppLayoutNavigation";
import type { NavItem } from "../nav/navConfig";

export interface NavigationDestination {
  /** Ruta a la que se navega (conserva la query del solicitante, p. ej. `?tab=fiscal`). */
  to: string;
  /** Etiqueta del NavItem del menú autenticado — el nombre de la pantalla no se duplica. */
  label: string;
}

/**
 * Destinos navegables derivados del menú autenticado (GET /api/v1/me/menu), el mismo árbol que ya
 * recibe RouteAccessGuard — sin otra consulta ni mapa de permisos propio. Garantía real del
 * contrato del menú: un NavItem presente significa que el usuario tiene el permiso de **vista** de
 * esa pantalla (NavItem.Permission); los permisos de edición no viajan en el DTO, así que un
 * destino disponible es "puede abrir la pantalla", nunca "puede editar". El backend sigue siendo
 * la autoridad final ([Authorize(Policy="perm:*")]).
 */
export interface NavigationAccess {
  /** false mientras el menú no se ha resuelto: ningún destino se considera disponible. */
  resolved: boolean;
  findDestination: (target: string) => NavigationDestination | null;
}

function pathOf(to: string): string {
  return to.split("#")[0].split("?")[0];
}

function collectLabels(items: NavItem[], acc: Map<string, string>): void {
  for (const item of items) {
    const path = item.to ? pathOf(item.to) : "";
    if (path && !acc.has(path)) acc.set(path, item.label);
    if (item.children?.length) collectLabels(item.children, acc);
  }
}

export function buildNavigationAccess(
  mainMenuGroups: MainMenuGroup[],
  resolved: boolean,
): NavigationAccess {
  if (!resolved) return { resolved, findDestination: () => null };

  // Coincidencia por ruta completa (sin query/hash), nunca por primer segmento: compartir el
  // prefijo /settings no da acceso a /settings/emission-points.
  const labels = new Map<string, string>();
  for (const group of mainMenuGroups) collectLabels(group.items, labels);

  return {
    resolved,
    findDestination: (target) => {
      const label = labels.get(pathOf(target));
      return label === undefined ? null : { to: target, label };
    },
  };
}

export const NavigationAccessContext = createContext<NavigationAccess>(
  buildNavigationAccess([], false),
);

export function useNavigationAccess(): NavigationAccess {
  return useContext(NavigationAccessContext);
}
