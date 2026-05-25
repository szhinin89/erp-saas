import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';
import { EmptyState, PageShell } from '../PageShell';
import { Card } from '../Card';
import { useI18n } from '../../i18n/i18n';
import { usePlatformGate } from '../../hooks/usePlatformGate';

export type PlatformPageTemplateProps = {
  title: string;
  /** Línea superior; por defecto grupo Home del menú. */
  kicker?: string;
  subtitle?: string;
  action?: ReactNode;
  /**
   * Si true (defecto), con sesión dentro de una empresa no se muestra el contenido:
   * pantalla “vuelva al panel global” con `subscriberGuardAction` o enlace por defecto.
   */
  requireGlobal?: boolean;
  /** Acción en barra del título cuando `requireGlobal` y el usuario está dentro de un subscriber. */
  subscriberGuardAction?: ReactNode;
  /** Clave i18n cuando el usuario no es operador platform. */
  accessDeniedKey?: string;
  /** Subtítulo en cabecera si acceso denegado; por defecto igual que `accessDeniedKey`. */
  accessDeniedSubtitleKey?: string;
  /** When true, renders children without the page header (kicker/title/subtitle). */
  hideHeader?: boolean;
  children: ReactNode;
};

/**
 * Plantilla común para pantallas platform: sin rol, sin contexto global (opcional), luego contenido.
 * En shell con topbar: preferir PlatformCrudTemplate (hideHeader) — docs/frontend-layout-conventions.md
 */
export function PlatformPageTemplate({
  title,
  kicker,
  subtitle,
  action,
  requireGlobal = true,
  subscriberGuardAction,
  accessDeniedKey = 'platform.noAccess',
  accessDeniedSubtitleKey,
  hideHeader = false,
  children,
}: PlatformPageTemplateProps) {
  const { t } = useI18n();
  const { isPlatformOperator, hasSelectedSubscriber } = usePlatformGate();
  const k = kicker ?? t('app.nav.group.home');

  if (!isPlatformOperator) {
    const deniedSubKey = accessDeniedSubtitleKey ?? accessDeniedKey;
    return (
      <PageShell kicker={k} title={title} subtitle={t(deniedSubKey)}>
        <Card>
          <EmptyState message={t(accessDeniedKey)} />
        </Card>
      </PageShell>
    );
  }

  if (requireGlobal && hasSelectedSubscriber) {
    const defaultAction = (
      <NavLink to="/platform/overview">{t('platform.backToGlobal')}</NavLink>
    );
    return (
      <PageShell kicker={k} title={title} action={subscriberGuardAction ?? defaultAction}>
        <Card>
          <EmptyState message={t('platform.alreadyInSubscriber')} />
        </Card>
      </PageShell>
    );
  }

  if (hideHeader) {
    return <>{children}</>;
  }

  return (
    <PageShell kicker={k} title={title} subtitle={subtitle} action={action}>
      {children}
    </PageShell>
  );
}
