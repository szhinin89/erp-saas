import { useRuntimeMode } from '../hooks/useRuntimeMode';
import { useI18n } from '../i18n/i18n';

/** Indica el modo runtime en cabeceras de dashboard (Platform / Subscriber / Company). */
export function RuntimeModeBadge() {
  const mode = useRuntimeMode();
  const { t } = useI18n();

  const label =
    mode === 'platform'
      ? t('app.runtimeMode.platform')
      : mode === 'subscriber'
        ? t('app.runtimeMode.subscriber')
        : mode === 'company'
          ? t('app.runtimeMode.company')
          : t('app.runtimeMode.unknown');

  return (
    <span className="badge badge--blue" title={t('app.runtimeMode.hint')}>
      {label}
    </span>
  );
}
