import { useRuntimeMode } from '../hooks/useRuntimeMode';
import { useI18n } from '../i18n/i18n';
import { Badge } from './PageShell';

/** Indica el modo runtime en cabeceras de dashboard (Platform / Tenant / Company). */
export function RuntimeModeBadge() {
  const mode = useRuntimeMode();
  const { t } = useI18n();

  const label =
    mode === 'company'
      ? t('app.runtimeMode.company')
      : t('app.runtimeMode.unknown');

  return <Badge label={label} variant="blue" title={t('app.runtimeMode.hint')} />;
}
