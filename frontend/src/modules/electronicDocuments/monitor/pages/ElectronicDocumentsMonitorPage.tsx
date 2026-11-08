import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { NoAccessPage } from "../../../../components/PageShell";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHSriServiceHealth } from "../../../../components/zh/ZHSriServiceHealth";
import { ZHTabBar, type ZHTab } from "../../../../components/zh/ZHTabBar";
import { ZHFieldHelp } from "../../../../components/zh/help";
import { HELP_KEYS } from "../../../../help";
import { useI18n } from "../../../../i18n/i18n";
import {
  useElectronicDocumentsMonitor,
  type ElectronicDocumentsMonitorTab,
} from "../hooks/useElectronicDocumentsMonitor";
import { ElectronicDocumentsSummaryTab } from "../components/ElectronicDocumentsSummaryTab";
import { ElectronicDocumentsFilters } from "../components/ElectronicDocumentsFilters";
import { ElectronicDocumentsTable } from "../components/ElectronicDocumentsTable";
import { ElectronicDocumentDetailPanel } from "../components/ElectronicDocumentDetailPanel";

export function ElectronicDocumentsMonitorPage() {
  const { t } = useI18n();
  const m = useElectronicDocumentsMonitor();

  if (!m.canView)
    return <NoAccessPage title={t("electronicDocuments.monitor.title")} />;

  const tabs: ZHTab<ElectronicDocumentsMonitorTab>[] = [
    {
      id: "summary",
      label: t("electronicDocuments.monitor.tabs.summary"),
      icon: "dashboard",
    },
    {
      id: "documents",
      label: t("electronicDocuments.monitor.tabs.documents"),
      icon: "receipt_long",
    },
    {
      id: "services",
      label: t("electronicDocuments.monitor.tabs.services"),
      mobileLabel: t("electronicDocuments.monitor.tabs.servicesShort"),
      icon: "cell_tower",
    },
  ];

  const loading =
    m.listLoading || m.dashboardLoading || m.attentionLoading;

  return (
    <ErpPageTemplate
      kicker={t("electronicDocuments.monitor.kicker")}
      title={t("electronicDocuments.monitor.title")}
      subtitle={t("electronicDocuments.monitor.subtitle")}
      action={
        <ZHBtn
          variant="secondary"
          type="button"
          disabled={loading}
          onClick={m.refresh}
        >
          <span className="material-symbols-outlined">refresh</span>
          {t("common.refresh")}
        </ZHBtn>
      }
    >
      {/* Salud compacta fuera de las pestañas, en su propia fila: la zona derecha del encabezado
          oficial no se encoge y en móvil recortaría los badges. */}
      <div className="zh-mb-8">
        <ZHSriServiceHealth variant="compact" />
      </div>
      {m.sriPaused && (
        <ZHPageNotice
          variant="neutral"
          icon="pause_circle"
          message={t("electronicDocuments.monitor.sriPaused.message")}
          detail={t("electronicDocuments.monitor.sriPaused.detail")}
        />
      )}

      <ZHTabBar
        tabs={tabs}
        activeTab={m.activeTab}
        onChange={m.setActiveTab}
        ariaLabel={t("electronicDocuments.monitor.tabs.ariaLabel")}
      />

      {m.activeTab === "summary" && (
        <ElectronicDocumentsSummaryTab
          dashboard={m.dashboard}
          dashboardLoading={m.dashboardLoading}
          dashboardError={m.dashboardError}
          attention={m.attention}
          attentionLoading={m.attentionLoading}
          attentionError={m.attentionError}
          activeState={m.filters.state}
          isTodayActive={m.isTodayFilterActive}
          onFilterState={m.applyStateFilter}
          onFilterToday={m.applyTodayFilter}
          onShowAllAttention={m.showAllAttention}
          onSelect={(id) => void m.openDetail(id)}
        />
      )}

      {m.activeTab === "documents" && (
        <div className="pg-section">
          <ElectronicDocumentsFilters
            dateFrom={m.filters.dateFrom}
            dateTo={m.filters.dateTo}
            state={m.filters.state}
            documentType={m.filters.documentType}
            environment={m.filters.environment}
            search={m.filters.search}
            onDateFromChange={(v) => {
              m.setDateFrom(v);
              m.setPage(1);
            }}
            onDateToChange={(v) => {
              m.setDateTo(v);
              m.setPage(1);
            }}
            onStateChange={(v) => {
              m.setState(v);
              m.setPage(1);
            }}
            onDocumentTypeChange={(v) => {
              m.setDocumentType(v);
              m.setPage(1);
            }}
            onEnvironmentChange={(v) => {
              m.setEnvironment(v);
              m.setPage(1);
            }}
            onSearchChange={(v) => {
              m.setSearch(v);
              m.setPage(1);
            }}
            onClear={m.resetFiltersAndReload}
            disabled={m.listLoading}
          />

          {m.listError && (
            <ZHPageNotice
              variant="error"
              message={t("electronicDocuments.monitor.table.loadError")}
              detail={m.listError}
            />
          )}

          <ElectronicDocumentsTable
            items={m.list?.items ?? []}
            loading={m.listLoading}
            total={m.list?.total ?? 0}
            page={m.page}
            pageSize={m.pageSize}
            onPageChange={m.setPage}
            onSelect={(id) => void m.openDetail(id)}
          />
        </div>
      )}

      {m.activeTab === "services" && (
        <ZHCard
          title={t("electronicDocuments.monitor.services.title")}
          actions={
            <ZHFieldHelp
              helpKey={HELP_KEYS.ELECTRONIC_DOCUMENTS_MONITOR_SRI_SERVICES}
            />
          }
        >
          <ZHSriServiceHealth variant="detail" />
        </ZHCard>
      )}

      <ElectronicDocumentDetailPanel
        open={m.selectedId !== null}
        detail={m.detail}
        loading={m.detailLoading}
        error={m.detailError}
        xmlContent={m.xmlContent}
        xmlVariant={m.xmlVariant}
        xmlLoading={m.xmlLoading}
        xmlError={m.xmlError}
        canRetry={m.canRetry}
        canReconcile={m.canReconcile}
        canRetransmitCorrected={m.canRetransmitCorrected}
        retryLoading={m.retryLoading}
        sriPaused={m.sriPaused}
        canDownloadRide={m.canDownloadRide}
        rideDownloading={m.rideDownloading}
        onClose={m.closeDetail}
        onViewXml={(variant) => void m.viewXml(variant)}
        onRetryNow={m.retryNow}
        onReconcile={m.reconcile}
        onRetransmitCorrected={m.retransmitCorrected}
        onDownloadRide={() => void m.downloadRide()}
      />
    </ErpPageTemplate>
  );
}
