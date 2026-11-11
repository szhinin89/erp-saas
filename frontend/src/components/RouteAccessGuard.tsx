import { useMemo, type ReactNode } from "react";
import { matchPath, useLocation } from "react-router-dom";
import { useI18n } from "../i18n/i18n";
import { LoadingState, NoAccessPage } from "./PageShell";
import {
  PROTECTED_REDIRECT_PATHS,
  PROTECTED_ROUTE_PATHS,
} from "../routes/protectedRoutePaths";
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

function cleanPath(path: string): string {
  const clean = path.split("?")[0].split("#")[0];
  return clean.length > 1 ? clean.replace(/\/+$/, "") : clean;
}

function firstSegment(path: string): string {
  const segment = cleanPath(path).split("/").filter(Boolean)[0];
  return segment ? `/${segment}` : "/";
}

/** Rutas protegidas sin ítem de menú propio que todo usuario autenticado puede abrir. */
const ALWAYS_ALLOWED_PATHS = new Set(["/", "/dashboard"]);

/**
 * IAM-V1-F2 — pantallas hijas con segmento literal que comparten EXACTAMENTE el permiso de su
 * pantalla padre del menú (verificado contra sus controllers): heredan su visibilidad. Una hija
 * con permiso propio (p. ej. /suppliers/credits, /sales/returns) nunca va aquí: necesita su
 * propio ítem de menú. Patrón registrado → ruta padre del menú.
 */
export const INHERITED_CHILD_ROUTES: Readonly<Record<string, string>> = {
  // Carga inicial: mismos permisos initialload.* que /initial-load.
  "/initial-load/customers": "/initial-load",
  "/initial-load/suppliers": "/initial-load",
  "/initial-load/products": "/initial-load",
  "/initial-load/initial-stock": "/initial-load",
  "/initial-load/initial-receivables": "/initial-load",
  "/initial-load/initial-payables": "/initial-load",
  // Devoluciones de compra: PurchaseReturnController exige purchases.view, igual que /purchases.
  "/purchases/returns": "/purchases",
  "/purchases/returns/:id": "/purchases",
};

/**
 * Sufijos que heredan la visibilidad de la pantalla del menú de la que cuelgan: el detalle
 * (`/:id`), el alta (`/new`) y la edición (`/:id/edit`) de esa misma pantalla.
 */
const INHERITING_SUFFIX = /^(?::[^/]+|new|:[^/]+\/edit)$/;

function collectNavRoutes(items: NavItem[], acc: Set<string>): void {
  for (const item of items) {
    if (item.to) acc.add(cleanPath(item.to));
    if (item.children?.length) collectNavRoutes(item.children, acc);
  }
}

/** Patrón registrado más específico (más segmentos literales) que coincide con la URL. */
export function resolveRoutePattern(
  pathname: string,
  registeredPaths: readonly string[],
): string | null {
  const clean = cleanPath(pathname);
  let best: string | null = null;
  let bestStatic = -1;
  for (const pattern of registeredPaths) {
    if (!matchPath({ path: pattern, end: true, caseSensitive: true }, clean)) continue;
    const staticCount = pattern.split("/").filter((s) => s && !s.startsWith(":")).length;
    if (staticCount > bestStatic) {
      best = pattern;
      bestStatic = staticCount;
    }
  }
  return best;
}

/**
 * True si `pathname` corresponde a una pantalla autorizada por el menú del usuario (permisos
 * efectivos resueltos por el backend). IAM-V1-F2: ya no basta compartir el primer segmento —
 * la URL debe ser una ruta del menú, su detalle/alta/edición, una hija que comparte permiso
 * (INHERITED_CHILD_ROUTES), un redirect registrado o una ruta siempre permitida.
 */
export function isRouteAllowed(
  pathname: string,
  mainMenuGroups: MainMenuGroup[],
  registeredPaths: readonly string[] = PROTECTED_ROUTE_PATHS,
  redirectPaths: ReadonlySet<string> = PROTECTED_REDIRECT_PATHS,
): boolean {
  const clean = cleanPath(pathname);
  if (ALWAYS_ALLOWED_PATHS.has(clean)) return true;
  if (LEGACY_REDIRECT_PREFIXES.has(firstSegment(clean))) return true;
  if (isLegacyRedirectAlias(clean)) return true;

  const navRoutes = new Set<string>();
  for (const group of mainMenuGroups) collectNavRoutes(group.items, navRoutes);
  if (navRoutes.has(clean)) return true;

  const pattern = resolveRoutePattern(clean, registeredPaths);
  if (!pattern) return false;
  if (navRoutes.has(pattern)) return true;
  // Un redirect solo monta un <Navigate>: el destino se valida de nuevo contra el menú.
  if (redirectPaths.has(pattern)) return true;

  const inheritedParent = INHERITED_CHILD_ROUTES[pattern];
  if (inheritedParent) return navRoutes.has(inheritedParent);

  for (const navRoute of navRoutes) {
    if (!pattern.startsWith(`${navRoute}/`)) continue;
    if (INHERITING_SUFFIX.test(pattern.slice(navRoute.length + 1))) return true;
  }
  return false;
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
 * navegación recibido del servidor (mainMenuGroups) y antes de resolver sesión y menú. El backend sigue siendo la
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
  const sessionLoading = useSessionStore((s) => s.isLoading);
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

  // IAM-V1-F2: ninguna página se monta antes de resolver el menú y los permisos de la sesión
  // (antes se montaba mientras cargaban y disparaba sus consultas). Si la sesión no pudo
  // cargarse, sin permisos efectivos no hay pantallas autorizadas (fail-closed).
  if (!sessionMenuResolved || (!sessionLoaded && sessionLoading)) {
    return <LoadingState />;
  }

  if (
    (!admin && !sessionLoaded) ||
    (!isRouteAllowed(location.pathname, mainMenuGroups) &&
      !isContextualRouteAllowed(location.pathname, effectivePermissions))
  ) {
    return <NoAccessPage title={t("app.accessDenied.title")} />;
  }

  return (
    <NavigationAccessContext.Provider value={navigationAccess}>
      {children}
    </NavigationAccessContext.Provider>
  );
}
