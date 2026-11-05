import { useMemo, type ReactNode } from "react";
import { matchPath, useLocation } from "react-router-dom";
import { useI18n } from "../i18n/i18n";
import { NoAccessPage } from "./PageShell";
import type { MainMenuGroup } from "./useAppLayoutNavigation";
import type { NavItem } from "../nav/navConfig";
import { NavigationAccessContext, buildNavigationAccess } from "./navigationAccess";
import { useAuthStore } from "../store/authStore";
import { useSessionStore } from "../store/sessionStore";
import { isAdminRole } from "../access/permissionUi";

/**
 * Rutas de redirección legacy (<Navigate replace>) cuyo primer segmento no coincide
 * con el destino canónico. Son alias de ruteo puro sin semántica de permisos:
 * se permiten para que el <Navigate> llegue a montar y redirija al destino real,
 * que sí se valida contra mainMenuGroups.
 */
const LEGACY_REDIRECT_PREFIXES = new Set([
  "/inventario",
  "/logistica",
  "/configuracion",
  "/actividad",
  "/security",
  // URLS-MENU-ALIGNMENT-01: ningún NavItem activo usa ya estos prefijos (todos sus ítems se
  // reubicaron bajo /products, /customers, /suppliers) — sin esto, isRouteAllowed bloquearía
  // el <Navigate> legacy antes de que llegue a montar y redirigir.
  "/masterdata",
  "/catalog",
  "/master",
  "/pricing",
]);

/**
 * NAV-COVERAGE-H4 — redirects legacy (<Navigate replace> / *LegacyRedirect) registrados en
 * routes/*.tsx cuyo primer segmento difiere del destino y no está en LEGACY_REDIRECT_PREFIXES.
 * Coincidencia EXACTA de ruta (matchPath, end + case-sensitive; nunca prefijo): solo deja montar el
 * redirect, sin acceso funcional; el destino canónico se vuelve a validar contra el menú tras el
 * <Navigate>. RouteAccessGuard.legacyAliases.test.tsx verifica que cada alias es un redirect
 * registrado y que ningún redirect que cambia de segmento queda sin cubrir.
 */
export const LEGACY_REDIRECT_ALIASES: readonly string[] = [
  "/admin/users", // → /access/users
  "/sales/payment-methods", // → /accounting/configuration/sales-collection-destinations
  "/cash", // → /treasury/cash
  "/cash/registers", // → /treasury/cash/registers
  "/finance/credit-terms", // → /suppliers/credit-terms
  "/finance/supplier-credits", // → /suppliers/credits
  "/finance/supplier-credits/:id", // → /suppliers/credits/:id
  "/inventory/items", // → /products/items
  "/inventory/item-types", // → /products/item-types
  "/settings/electronic-invoicing", // → /sri/configuration/electronic-invoicing
  "/electronic-documents/monitor", // → /sri/electronic-documents/monitor
];

function isLegacyRedirectAlias(pathname: string): boolean {
  const clean = pathname.split("?")[0].split("#")[0];
  return LEGACY_REDIRECT_ALIASES.some(
    (path) => matchPath({ path, end: true, caseSensitive: true }, clean) !== null,
  );
}

function firstSegment(path: string): string {
  const clean = path.split("?")[0].split("#")[0];
  const segment = clean.split("/").filter(Boolean)[0];
  return segment ? `/${segment}` : "/";
}

function collectAllowedPrefixes(items: NavItem[], acc: Set<string>): void {
  for (const item of items) {
    if (item.to) acc.add(firstSegment(item.to));
    if (item.children?.length) collectAllowedPrefixes(item.children, acc);
  }
}

/** True si `pathname` cae dentro del árbol de navegación permitido por el backend. */
export function isRouteAllowed(
  pathname: string,
  mainMenuGroups: MainMenuGroup[],
): boolean {
  const segment = firstSegment(pathname);

  if (segment === "/") return true;
  if (LEGACY_REDIRECT_PREFIXES.has(segment)) return true;
  if (isLegacyRedirectAlias(pathname)) return true;

  const allowed = new Set<string>();
  for (const group of mainMenuGroups) {
    collectAllowedPrefixes(group.items, allowed);
  }
  return allowed.has(segment);
}

/**
 * NAV-COVERAGE-H1 — pantallas a las que se llega desde el flujo de otro módulo sin NavItem propio
 * para ese usuario. Cada entrada es una ruta EXACTA (nunca un prefijo) más el permiso efectivo que
 * el backend ya exige para ese flujo; no concede acceso a datos: la API sigue filtrando por
 * ownership ([Authorize(Policy="perm:*")] + Application).
 *
 * Solicitudes de efectivo: el solicitante (`supplier-payments.create`, mismo permiso de
 * POST/GET mine/cancel en CashFundingRequestsController) abre "Mis solicitudes" y el detalle de la
 * suya; GET {id} devuelve 404 si no es suya ni tiene `caja.funding-requests.view`.
 */
const CONTEXTUAL_ROUTES: ReadonlyArray<{ pattern: RegExp; permission: string }> = [
  {
    pattern: /^\/treasury\/cash\/funding-requests(?:\/[^/]+)?\/?$/,
    permission: "supplier-payments.create",
  },
];

/**
 * True si `pathname` es una ruta contextual exacta y el usuario tiene su permiso efectivo.
 * Fail-closed: sin permisos cargados no se concede nada.
 */
export function isContextualRouteAllowed(
  pathname: string,
  permissions: readonly string[],
): boolean {
  const clean = pathname.split("?")[0].split("#")[0];
  return CONTEXTUAL_ROUTES.some(
    (route) =>
      route.pattern.test(clean) &&
      (permissions.includes("*") || permissions.includes(route.permission)),
  );
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
  const admin = isAdminRole(useAuthStore((s) => s.user?.role));
  const sessionLoaded = useSessionStore((s) => s.isLoaded);
  const permissions = useSessionStore((s) => s.authorization?.permissions);
  // Fail-closed: sin sesión cargada no hay permisos efectivos (Admin = comodín).
  let effectivePermissions: readonly string[] = [];
  if (admin) effectivePermissions = ["*"];
  else if (sessionLoaded) effectivePermissions = permissions ?? [];
  // Mismo menú ya resuelto, expuesto a las páginas (p. ej. acciones de Readiness) sin otra consulta.
  const navigationAccess = useMemo(
    () => buildNavigationAccess(mainMenuGroups, sessionMenuResolved),
    [mainMenuGroups, sessionMenuResolved],
  );

  if (
    sessionMenuResolved &&
    !isRouteAllowed(location.pathname, mainMenuGroups) &&
    !isContextualRouteAllowed(location.pathname, effectivePermissions)
  ) {
    return <NoAccessPage title={t("app.accessDenied.title")} />;
  }

  return (
    <NavigationAccessContext.Provider value={navigationAccess}>
      {children}
    </NavigationAccessContext.Provider>
  );
}
