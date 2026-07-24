import { Badge } from '../../PageShell';
import { useI18n } from '../../../i18n/i18n';
import { electronicDocumentStateBadgeVariant, electronicDocumentStateIcon } from '../../../modules/electronicDocuments/monitor/utils/stateBadge';

type Props = { currentState: string };

/** Badge de estado reutilizable — mismo mapeo estado→color/ícono que ya usa el Monitor. */
export function ElectronicDocumentStatusBadge({ currentState }: Props) {
  const { t } = useI18n();
  return (
    <Badge
      variant={electronicDocumentStateBadgeVariant(currentState)}
      label={
        <span className="edm-state-badge-label">
          <span className="material-symbols-outlined zh-icon-sm">{electronicDocumentStateIcon(currentState)}</span>
          {t(`electronicDocuments.monitor.state.${currentState}`)}
        </span>
      }
    />
  );
}
