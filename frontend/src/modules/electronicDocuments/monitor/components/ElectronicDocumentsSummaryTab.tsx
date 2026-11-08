import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHFieldHelp } from "../../../../components/zh/help";
import { HELP_KEYS } from "../../../../help";
import { useI18n } from "../../../../i18n/i18n";
import type {
  ElectronicDocumentsDashboardDto,
  ElectronicDocumentsListResponse,
} from "../api/electronicDocumentsMonitorService";
import { ElectronicDocumentsDashboardCards } from "./ElectronicDocumentsDashboardCards";
import { ElectronicDocumentsTable } from "./ElectronicDocumentsTable";

type Props = {
  dashboard: ElectronicDocumentsDashboardDto | null;
  dashboardLoading: boolean;
  dashboardError: string | null;
  attention: ElectronicDocumentsListResponse | null;
  attentionLoading: boolean;
  attentionError: string | null;
  activeState: string;
  isTodayActive: boolean;
  onFilterState: (value: string) => void;
  onFilterToday: () => void;
  onShowAllAttention: () => void;
  onSelect: (id: string) => void;
};

/** Pestaña Resumen del Monitor: indicadores agrupados y los documentos más recientes que requieren atención. */
export function ElectronicDocumentsSummaryTab({
  dashboard,
  dashboardLoading,
  dashboardError,
  attention,
  attentionLoading,
  attentionError,
  activeState,
  isTodayActive,
  onFilterState,
  onFilterToday,
  onShowAllAttention,
  onSelect,
}: Props) {
  const { t } = useI18n();
  const attentionTotal = attention?.total ?? 0;

  return (
    <>
      {dashboardError && (
        <ZHPageNotice
          variant="error"
          message={t("electronicDocuments.monitor.dashboard.loadError")}
          detail={dashboardError}
        />
      )}
      <ElectronicDocumentsDashboardCards
        dashboard={dashboard}
        loading={dashboardLoading}
        activeState={activeState}
        isTodayActive={isTodayActive}
        onFilterState={onFilterState}
        onFilterToday={onFilterToday}
      />

      <ZHCard
        title={t("electronicDocuments.monitor.attention.title")}
        actions={
          <div className="zh-form-actions-row zh-form-actions-row--flush">
            <ZHFieldHelp
              helpKey={HELP_KEYS.ELECTRONIC_DOCUMENTS_MONITOR_ATTENTION}
            />
            {attentionTotal > 0 && (
              <ZHBtn
                variant="secondary"
                size="sm"
                type="button"
                onClick={onShowAllAttention}
              >
                {t("electronicDocuments.monitor.attention.viewAll", {
                  count: attentionTotal,
                })}
              </ZHBtn>
            )}
          </div>
        }
      >
        {attentionError && (
          <ZHPageNotice
            variant="error"
            message={t("electronicDocuments.monitor.attention.loadError")}
            detail={attentionError}
          />
        )}
        <ElectronicDocumentsTable
          variant="attention"
          items={attention?.items ?? []}
          loading={attentionLoading}
          onSelect={onSelect}
          emptyMessage={t("electronicDocuments.monitor.attention.empty")}
        />
      </ZHCard>
    </>
  );
}
