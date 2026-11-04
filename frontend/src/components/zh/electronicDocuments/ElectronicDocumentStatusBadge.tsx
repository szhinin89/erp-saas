import { Badge } from "../../PageShell";
import { useI18n } from "../../../i18n/i18n";
import {
  electronicDocumentStateBadgeVariant,
  electronicDocumentStateHelpKey,
  electronicDocumentStateIcon,
} from "../../../modules/electronicDocuments/monitor/utils/stateBadge";
import "./electronic-documents.css";

type Props = {
  currentState: string;
  /** Muestra, además del tooltip, la explicación breve del estado como texto visible. */
  showHelp?: boolean;
};

/** Badge de estado reutilizable — mismo mapeo estado→color/ícono que ya usa el Monitor. */
export function ElectronicDocumentStatusBadge({ currentState, showHelp }: Props) {
  const { t } = useI18n();
  const helpKey = electronicDocumentStateHelpKey(currentState);
  const help = helpKey ? t(helpKey) : undefined;
  const badge = (
    <Badge
      variant={electronicDocumentStateBadgeVariant(currentState)}
      title={help}
      label={
        <span className="zh-edoc-state-badge-label">
          <span className="material-symbols-outlined zh-icon-sm">
            {electronicDocumentStateIcon(currentState)}
          </span>
          {t(`electronicDocuments.monitor.state.${currentState}`)}
        </span>
      }
    />
  );
  if (!showHelp || !help) return badge;
  return (
    <>
      {badge}
      <span className="zh-edoc-state-help">{help}</span>
    </>
  );
}
