import { ReportKpiCard } from "../../../../components/ReportPageTemplate";
import { ZHGrid } from "../../../../components/zh/ZHForm";
import { useI18n } from "../../../../i18n/i18n";
import type { ElectronicDocumentsDashboardDto } from "../api/electronicDocumentsMonitorService";
import "./electronic-documents-monitor.css";

type Props = {
  dashboard: ElectronicDocumentsDashboardDto | null;
  loading: boolean;
  activeState: string;
  isTodayActive: boolean;
  onFilterState: (value: string) => void;
  onFilterToday: () => void;
};

const PENDING_STATES = "Draft,XmlGenerated";
const IN_PROGRESS_STATES = "Signed,Sent,Received";
const ERROR_STATES = "DeadLetter,Failed";

function formatMinutes(minutes: number | null, naLabel: string): string {
  if (minutes === null) return naLabel;
  if (minutes < 60) return `${Math.round(minutes)} min`;
  return `${(minutes / 60).toFixed(1)} h`;
}

export function ElectronicDocumentsDashboardCards({
  dashboard,
  loading,
  activeState,
  isTodayActive,
  onFilterState,
  onFilterToday,
}: Props) {
  const { t } = useI18n();
  const naLabel = t("electronicDocuments.monitor.dashboard.notAvailable");

  const value = (n: number | undefined) =>
    loading || dashboard === null ? "—" : (n ?? 0).toString();

  return (
    <>
      <ZHGrid cols={3}>
        <ReportKpiCard
          label={t("electronicDocuments.monitor.dashboard.pending")}
          value={value(dashboard?.pending)}
          onClick={() => onFilterState(PENDING_STATES)}
          active={activeState === PENDING_STATES}
        />
        <ReportKpiCard
          label={t("electronicDocuments.monitor.dashboard.inProgress")}
          value={value(
            dashboard ? dashboard.signed + dashboard.sent : undefined,
          )}
          onClick={() => onFilterState(IN_PROGRESS_STATES)}
          active={activeState === IN_PROGRESS_STATES}
        />
        <ReportKpiCard
          label={t("electronicDocuments.monitor.dashboard.authorized")}
          value={value(dashboard?.authorized)}
          valueTone="success"
          onClick={() => onFilterState("Authorized")}
          active={activeState === "Authorized"}
        />
      </ZHGrid>

      <ZHGrid cols={3}>
        <ReportKpiCard
          label={t("electronicDocuments.monitor.dashboard.rejected")}
          value={value(dashboard?.rejected)}
          valueTone="danger"
          onClick={() => onFilterState("Rejected")}
          active={activeState === "Rejected"}
        />
        <ReportKpiCard
          label={t("electronicDocuments.monitor.dashboard.error")}
          value={value(dashboard?.error)}
          valueTone="danger"
          onClick={() => onFilterState(ERROR_STATES)}
          active={activeState === ERROR_STATES}
        />
        <ReportKpiCard
          label={t("electronicDocuments.monitor.dashboard.totalToday")}
          value={value(dashboard?.totalToday)}
          onClick={onFilterToday}
          active={isTodayActive}
        />
      </ZHGrid>

      <ZHGrid cols={3}>
        <ReportKpiCard
          label={t("electronicDocuments.monitor.dashboard.authorizedToday")}
          value={value(dashboard?.authorizedToday)}
          valueTone="success"
        />
        <ReportKpiCard
          label={t("electronicDocuments.monitor.dashboard.errorsToday")}
          value={value(dashboard?.errorsToday)}
          valueTone="danger"
        />
        <ReportKpiCard
          label={t("electronicDocuments.monitor.dashboard.pendingRetries")}
          value={value(dashboard?.pendingRetries)}
        />
      </ZHGrid>

      <ZHGrid cols={3}>
        <ReportKpiCard
          label={t(
            "electronicDocuments.monitor.dashboard.averageAuthorizationTime",
          )}
          value={
            loading || dashboard === null
              ? "—"
              : formatMinutes(dashboard.averageAuthorizationMinutes, naLabel)
          }
        />
      </ZHGrid>
    </>
  );
}
