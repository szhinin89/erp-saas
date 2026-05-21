import { PageShell, EmptyState } from '../../../components/PageShell';
import { useI18n } from '../../../i18n/i18n';

type ModulePlaceholderVariant = 'purchases' | 'hr';

const titleKey: Record<ModulePlaceholderVariant, string> = {
  purchases: 'app.nav.group.purchases',
  hr: 'app.nav.group.hr',
};

export function ModulePlaceholderPage(props: { variant: ModulePlaceholderVariant }) {
  const { t } = useI18n();
  const tk = titleKey[props.variant];
  return (
    <PageShell
      kicker={t('app.nav.modulePlaceholder.kicker')}
      title={t(tk)}
      subtitle={t('app.nav.modulePlaceholder.subtitle')}
    >
      <EmptyState message={t('app.nav.modulePlaceholder.body')} />
    </PageShell>
  );
}
