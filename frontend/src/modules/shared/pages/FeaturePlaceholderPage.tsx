import { useLocation } from 'react-router-dom';
import { PageShell, EmptyState } from '../../../components/PageShell';
import { useI18n } from '../../../i18n/i18n';

/** Placeholder for menu routes without a dedicated screen yet. */
export function FeaturePlaceholderPage() {
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
