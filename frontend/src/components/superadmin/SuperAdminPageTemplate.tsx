import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';
import { EmptyState, PageShell, TableCard } from '../PageShell';
import { useI18n } from '../../i18n/i18n';
import { useSuperAdminGate } from '../../hooks/useSuperAdminGate';

export type SuperAdminPageTemplateProps = {
  title: string;
  /** Línea superior; por defecto grupo Home del menú. */
  kicker?: string;
  subtitle?: string;
  action?: ReactNode;
  /**
   * Si true (defecto), con sesión dentro de una empresa no se muestra el contenido:
   * pantalla “vuelva al panel global” con `tenantGuardAction` o enlace por defecto.
   */
  requireGlobal?: boolean;
  /** Acción en barra del título cuando `requireGlobal` y el usuario está dentro de un tenant. */
  tenantGuardAction?: ReactNode;
  /** Clave i18n cuando el rol no es SuperAdmin. */
  accessDeniedKey?: string;
  /** Subtítulo en cabecera si acceso denegado; por defecto igual que `accessDeniedKey`. */
  accessDeniedSubtitleKey?: string;
  children: ReactNode;
};

/**
 * Plantilla común para pantallas SuperAdmin: sin rol, sin contexto global (opcional), luego contenido.
 */
export function SuperAdminPageTemplate({
  title,
  kicker,
  subtitle,
  action,
  requireGlobal = true,
  tenantGuardAction,
  accessDeniedKey = 'superadmin.noAccess',
  accessDeniedSubtitleKey,
  children,
}: SuperAdminPageTemplateProps) {
  const { t } = useI18n();
  const { isSuperAdmin, hasSelectedTenant } = useSuperAdminGate();
  const k = kicker ?? t('app.nav.group.home');

  if (!isSuperAdmin) {
    const deniedSubKey = accessDeniedSubtitleKey ?? accessDeniedKey;
    return (
      <PageShell kicker={k} title={title} subtitle={t(deniedSubKey)}>
        <TableCard>
          <EmptyState message={t(accessDeniedKey)} />
        </TableCard>
      </PageShell>
    );
  }

  if (requireGlobal && hasSelectedTenant) {
    const defaultAction = (
      <NavLink to="/superadmin/overview">{t('superadmin.backToGlobal')}</NavLink>
    );
    return (
      <PageShell kicker={k} title={title} action={tenantGuardAction ?? defaultAction}>
        <TableCard>
          <EmptyState message={t('superadmin.alreadyInTenant')} />
        </TableCard>
      </PageShell>
    );
  }

  return (
    <PageShell kicker={k} title={title} subtitle={subtitle} action={action}>
      {children}
    </PageShell>
  );
}
