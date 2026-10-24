import { ZHLineIssuesTag } from "../../../components/zh/notices";
import { useI18n } from "../../../i18n/i18n";
import type { NoticeSeverity, NoticeVM } from "../../../notices";

/**
 * Adaptador único de Compras para ZHLineIssuesTag (líneas de la compra y productos pendientes).
 * Purchase labels only; priority and corrective actions are supplied by the caller.
 */
export function PurchaseLineIssuesTag({ issues }: { issues: NoticeVM[] }) {
  const { t } = useI18n();
  const severityWord: Record<NoticeSeverity, string> = {
    danger: t("purchases.lines.issueSeverity.danger", "Bloqueante"),
    warning: t("purchases.lines.issueSeverity.warning", "Advertencia"),
    info: t("purchases.lines.issueSeverity.info", "Aviso"),
    success: "",
  };
  return <ZHLineIssuesTag
    issues={issues}
    label={(top, additionalCount) => `${severityWord[top.severity]}: ${top.label}${additionalCount > 0 ? ` · ${t("purchases.lines.issuesMore", { count: additionalCount })}` : ""}`}
    title={(count) => t("purchases.lines.issuesTitle", { count })}
  />;
}
