import { lazy, Suspense, type ComponentType } from "react";
import { LoadingState } from "../components/PageShell";

/**
 * Code-splitting por ruta: import dinámico de un export nombrado de página.
 * Mantiene la misma firma del componente para React Router.
 */
export function lazyNamedPage<P extends object = Record<string, never>>(
  factory: () => Promise<Record<string, ComponentType<P>>>,
  exportName: string,
) {
  const LazyPage = lazy(() =>
    factory().then((module) => ({
      default: module[exportName] as ComponentType<P>,
    })),
  );

  return function LazyRoutePage(props: P) {
    return (
      <Suspense fallback={<LoadingState />}>
        <LazyPage {...props} />
      </Suspense>
    );
  };
}

/** Import dinámico cuando el módulo exporta `default`. */
export function lazyDefaultPage<P extends object = Record<string, never>>(
  factory: () => Promise<{ default: ComponentType<P> }>,
) {
  const LazyPage = lazy(factory);

  return function LazyRoutePage(props: P) {
    return (
      <Suspense fallback={<LoadingState />}>
        <LazyPage {...props} />
      </Suspense>
    );
  };
}
