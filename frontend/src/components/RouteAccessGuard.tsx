import type { ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { useI18n } from '../i18n/i18n';
import { NoAccessPage } from './PageShell';
import type { MainMenuGroup } from './useAppLayoutNavigation';
import type { NavItem } from '../nav/navConfig';

/**
 * Rutas de redirección legacy (<Navigate replace>) cuyo primer segmento no coincide
 * con el destino canónico. Son alias de ruteo puro sin semántica de permisos:
 * se permiten para que el <Navigate> llegue a montar y redirija al destino real,
 * que sí se valida contra mainMenuGroups.
 */
const LEGACY_REDIRECT_PREFIXES = new Set([
  '/inventario',
  '/logistica',
  '/configuracion',
  '/actividad',
  '/security',
]);

function firstSegment(path: string): string {
  const clean = path.split('?')[0].split('#')[0];
  const segment = clean.split('/').filter(Boolean)[0];
  return segment ? `/${segment}` : '/';
}

function collectAllowedPrefixes(items: NavItem[], acc: Set<string>): void {
  for (const item of items) {
    if (item.to) acc.add(firstSegment(item.to));
    if (item.children?.length) collectAllowedPrefixes(item.children, acc);
  }
}

/** True si `pathname` cae dentro del árbol de navegación permitido por el backend. */
export function isRouteAllowed(pathname: string, mainMenuGroups: MainMenuGroup[]): boolean {
  const segment = firstSegment(pathname);

  if (segment === '/') return true;
  if (LEGACY_REDIRECT_PREFIXES.has(segment)) return true;

  const allowed = new Set<string>();
  for (const group of mainMenuGroups) {
    collectAllowedPrefixes(group.items, allowed);
  }
  return allowed.has(segment);
}

/**
 * Defensa en profundidad (Nivel 2): impide montar páginas fuera del árbol de
 * navegación recibido del servidor (mainMenuGroups). El backend sigue siendo la
 * autoridad final vía [Authorize(Policy="perm:*")].
 */
export function RouteAccessGuard({
  mainMenuGroups,
  sessionMenuResolved,
  children,
}: {
  mainMenuGroups: MainMenuGroup[];
  sessionMenuResolved: boolean;
  children: ReactNode;
}) {
  const location = useLocation();
  const { t } = useI18n();

  if (sessionMenuResolved && !isRouteAllowed(location.pathname, mainMenuGroups)) {
    return <NoAccessPage title={t('app.accessDenied.title')} />;
  }

  return <>{children}</>;
}
