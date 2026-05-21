import { lazy, Suspense, type ComponentType } from 'react';
import { LoadingState } from '../components/PageShell';

/**
 * Code-splitting por ruta: import dinámico de un export nombrado de página.
 * Mantiene la misma firma del componente para React Router.
 */
export function lazyNamedPage<M extends Record<string, ComponentType<object>>>(
  factory: () => Promise<M>,
  exportName: keyof M & string,
) {
  const LazyPage = lazy(() =>
    factory().then((module) => ({
      default: module[exportName] as ComponentType<object>,
    })),
  );

  return function LazyRoutePage(props: object) {
    return (
      <Suspense fallback={<LoadingState />}>
        <LazyPage {...props} />
      </Suspense>
    );
  };
}

/** Import dinámico cuando el módulo exporta `default`. */
export function lazyDefaultPage(factory: () => Promise<{ default: ComponentType<object> }>) {
  const LazyPage = lazy(factory);

  return function LazyRoutePage(props: object) {
    return (
      <Suspense fallback={<LoadingState />}>
        <LazyPage {...props} />
      </Suspense>
    );
  };
}
