import { ReportKpiCard } from "../../../../components/ReportPageTemplate";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHFieldHelp } from "../../../../components/zh/help";
import { HELP_KEYS } from "../../../../help";
import { useI18n } from "../../../../i18n/i18n";
import type { ElectronicDocumentsDashboardDto } from "../api/electronicDocumentsMonitorService";
import { ELECTRONIC_DOCUMENT_STATE_GROUPS } from "../utils/enumOptions";

type Props = {
  dashboard: ElectronicDocumentsDashboardDto | null;
  loading: boolean;
  activeState: string;
  isTodayActive: boolean;
  onFilterState: (value: string) => void;
  onFilterToday: () => void;
};

const GROUPS = ELECTRONIC_DOCUMENT_STATE_GROUPS;

/**
 * Los diez indicadores de `GET /electronic-documents/dashboard`, agrupados en Estado actual, Hoy y
 * Seguimiento. Cada grupo es un `ZHCard` con su ayuda oficial y el grid `.pg-kpis`. Los valores y
 * agrupaciones de estados son los del backend: la UI no recalcula ni reinterpreta.
 */
export function ElectronicDocumentsDashboardCards({
  dashboard,
  loading,
  activeState,
  isTodayActive,
  onFilterState,
  onFilterToday,
}: Props) {
  const { t } = useI18n();
  const ready = !loading && dashboard !== null;

  const value = (n: number | undefined) => (ready ? (n ?? 0).toString() : "—");

  const average = () => {
    const minutes = dashboard?.averageAuthorizationMinutes ?? null;
    if (!ready) return { value: "—", unit: undefined };
    if (minutes === null)
      return {
        value: t("electronicDocuments.monitor.dashboard.notAvailable"),
        unit: undefined,
      };
    if (minutes < 60)
      return {
        value: Math.round(minutes).toString(),
        unit: t("electronicDocuments.monitor.units.minutes"),
      };
    return {
      value: (minutes / 60).toFixed(1),
      unit: t("electronicDocuments.monitor.units.hours"),
    };
  };
  const averageValue = average();

  return (
    <>
      <ZHCard
        className="zh-mb-16"
        title={t("electronicDocuments.monitor.summary.currentState")}
        actions={
          <ZHFieldHelp
            helpKey={HELP_KEYS.ELECTRONIC_DOCUMENTS_MONITOR_CURRENT_STATE}
          />
        }
      >
        <div className="pg-kpis">
          <ReportKpiCard
            icon="schedule"
            tone="neutral"
            label={t(GROUPS.pending.labelKey)}
            value={value(dashboard?.pending)}
            onClick={() => onFilterState(GROUPS.pending.value)}
            active={activeState === GROUPS.pending.value}
          />
          <ReportKpiCard
            icon="hourglass_top"
            tone="info"
            label={t(GROUPS.inProgress.labelKey)}
            value={value(
              dashboard ? dashboard.signed + dashboard.sent : undefined,
            )}
            onClick={() => onFilterState(GROUPS.inProgress.value)}
            active={activeState === GROUPS.inProgress.value}
          />
          <ReportKpiCard
            icon="check_circle"
            tone="success"
            label={t("electronicDocuments.monitor.dashboard.authorized")}
            value={value(dashboard?.authorized)}
            valueTone="success"
            onClick={() => onFilterState("Authorized")}
            active={activeState === "Authorized"}
          />
          <ReportKpiCard
            icon="report"
            tone="error"
            label={t(GROUPS.error.labelKey)}
            value={value(dashboard?.error)}
            valueTone="danger"
            onClick={() => onFilterState(GROUPS.error.value)}
            active={activeState === GROUPS.error.value}
          />
        </div>
      </ZHCard>

      <ZHCard
        className="zh-mb-16"
        title={t("electronicDocuments.monitor.summary.today")}
        actions={
          <ZHFieldHelp helpKey={HELP_KEYS.ELECTRONIC_DOCUMENTS_MONITOR_TODAY} />
        }
      >
        <div className="pg-kpis">
          <ReportKpiCard
            icon="note_add"
            tone="primary"
            label={t("electronicDocuments.monitor.dashboard.totalToday")}
            value={value(dashboard?.totalToday)}
            onClick={onFilterToday}
            active={isTodayActive}
          />
          <ReportKpiCard
            icon="task_alt"
            tone="success"
            label={t("electronicDocuments.monitor.dashboard.authorizedToday")}
            value={value(dashboard?.authorizedToday)}
            valueTone="success"
          />
          <ReportKpiCard
            icon="error"
            tone="error"
            label={t("electronicDocuments.monitor.dashboard.errorsToday")}
            value={value(dashboard?.errorsToday)}
            valueTone="danger"
          />
        </div>
      </ZHCard>

      <ZHCard
        className="zh-mb-16"
        title={t("electronicDocuments.monitor.summary.followUp")}
        actions={
          <ZHFieldHelp
            helpKey={HELP_KEYS.ELECTRONIC_DOCUMENTS_MONITOR_FOLLOW_UP}
          />
        }
      >
        <div className="pg-kpis">
          <ReportKpiCard
            icon="cancel"
            tone="error"
            label={t("electronicDocuments.monitor.dashboard.rejected")}
            value={value(dashboard?.rejected)}
            valueTone="danger"
            onClick={() => onFilterState("Rejected")}
            active={activeState === "Rejected"}
          />
          <ReportKpiCard
            icon="autorenew"
            tone="warning"
            label={t("electronicDocuments.monitor.dashboard.pendingRetries")}
            value={value(dashboard?.pendingRetries)}
          />
          <ReportKpiCard
            icon="timer"
            tone="neutral"
            label={t(
              "electronicDocuments.monitor.dashboard.averageAuthorizationTime",
            )}
            value={averageValue.value}
            unit={averageValue.unit}
            sub={
              <p className="subtle">
                {t("electronicDocuments.monitor.dashboard.historical")}
              </p>
            }
          />
        </div>
      </ZHCard>
    </>
  );
}
