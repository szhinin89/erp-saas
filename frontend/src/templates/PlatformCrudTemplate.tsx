import {
  PlatformPageTemplate,
  type PlatformPageTemplateProps,
} from '../components/platform/PlatformPageTemplate';

export type PlatformCrudTemplateProps = PlatformPageTemplateProps & {
  /**
   * En rutas bajo PlatformLayout el título va en `sa-topbar`.
   * Default true → `hideHeader` salvo que se pase explícitamente.
   */
  shellLayout?: boolean;
  /** Clase extra en el wrapper `.pg-page` cuando el header del shell está activo. */
  pageClassName?: string;
};

/**
 * Plantilla CRUD/listado platform: guards de rol/subscriber + PageShell opcional.
 * Usar en todas las rutas `/platform/*` (LayoutFrame ya está en PlatformLayout).
 *
 * @see docs/frontend-layout-conventions.md
 */
export function PlatformCrudTemplate({
  shellLayout = true,
  hideHeader,
  pageClassName,
  children,
  ...templateProps
}: PlatformCrudTemplateProps) {
  const suppressHeader = hideHeader ?? shellLayout;

  return (
    <PlatformPageTemplate {...templateProps} hideHeader={suppressHeader}>
      {suppressHeader ? (
        <div className={['pg-page', pageClassName].filter(Boolean).join(' ')}>{children}</div>
      ) : (
        children
      )}
    </PlatformPageTemplate>
  );
}
