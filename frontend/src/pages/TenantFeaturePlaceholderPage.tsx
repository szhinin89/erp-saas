import { useLocation } from 'react-router-dom';
import { PageShell, EmptyState } from '../components/PageShell';
import { useI18n } from '../i18n/i18n';

/** Pantalla temporal para rutas del catálogo de menú que aún no tienen UI en el SPA. */
export function TenantFeaturePlaceholderPage() {
  const { pathname } = useLocation();
  const { t } = useI18n();
  return (
    <PageShell
      kicker={t('app.nav.modulePlaceholder.kicker')}
      title={pathname}
      subtitle={t('app.nav.modulePlaceholder.subtitle')}
    >
      <EmptyState message={t('app.nav.modulePlaceholder.body')} />
    </PageShell>
  );
}
