import {
  SuperAdminPageTemplate,
  type SuperAdminPageTemplateProps,
} from '../components/superadmin/SuperAdminPageTemplate';

export type SuperAdminCrudTemplateProps = SuperAdminPageTemplateProps & {
  /**
   * En rutas bajo SuperAdminLayout el título va en `sa-topbar`.
   * Default true → `hideHeader` salvo que se pase explícitamente.
   */
  shellLayout?: boolean;
  /** Clase extra en el wrapper `.pg-page` cuando el header del shell está activo. */
  pageClassName?: string;
};

/**
 * Plantilla CRUD/listado SuperAdmin: guards de rol/subscriber + PageShell opcional.
 * Usar en todas las rutas `/superadmin/*` (LayoutFrame ya está en SuperAdminLayout).
 *
 * @see docs/frontend-layout-conventions.md
 */
export function SuperAdminCrudTemplate({
  shellLayout = true,
  hideHeader,
  pageClassName,
  children,
  ...templateProps
}: SuperAdminCrudTemplateProps) {
  const suppressHeader = hideHeader ?? shellLayout;

  return (
    <SuperAdminPageTemplate {...templateProps} hideHeader={suppressHeader}>
      {suppressHeader ? (
        <div className={['pg-page', pageClassName].filter(Boolean).join(' ')}>{children}</div>
      ) : (
        children
      )}
    </SuperAdminPageTemplate>
  );
}
