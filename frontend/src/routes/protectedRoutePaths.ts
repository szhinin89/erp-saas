import { isValidElement, type ReactElement } from "react";
import { Navigate } from "react-router-dom";
import { accessRoutes } from "./accessRoutes";
import { catalogRoutes } from "./catalogRoutes";
import { mainRoutes } from "./mainRoutes";
import { SupplierCreditDetailLegacyRedirect } from "./legacyRedirects";

/**
 * IAM-V1-F2 — patrones de las rutas protegidas (las que monta AppLayout) derivados de los mismos
 * arreglos de <Route> que registra App.tsx: nunca una lista paralela. RouteAccessGuard los usa
 * para resolver qué patrón corresponde a una URL y decidir si hereda la visibilidad de su
 * pantalla del menú.
 */
type RouteEl = ReactElement<{ path?: string; element?: ReactElement }>;

const PROTECTED_ROUTES = [
  ...mainRoutes,
  ...catalogRoutes,
  ...accessRoutes,
] as RouteEl[];

function pathsOf(predicate: (element: ReactElement | undefined) => boolean): string[] {
  const paths = new Set<string>();
  for (const route of PROTECTED_ROUTES) {
    if (!isValidElement(route)) continue;
    const { path, element } = route.props;
    if (path && predicate(element)) paths.add(path);
  }
  return [...paths];
}

const isRedirect = (element: ReactElement | undefined) =>
  !!element &&
  (element.type === Navigate || element.type === SupplierCreditDetailLegacyRedirect);

/** Todos los patrones registrados bajo AppLayout. */
export const PROTECTED_ROUTE_PATHS: readonly string[] = pathsOf(() => true);

/**
 * Rutas que solo redirigen (<Navigate replace> o *LegacyRedirect): pueden montarse para
 * redirigir; el destino se vuelve a validar contra el menú.
 */
export const PROTECTED_REDIRECT_PATHS: ReadonlySet<string> = new Set(pathsOf(isRedirect));
