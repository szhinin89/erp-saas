import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { NoAccessPage } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../../i18n/i18n';
import { useElectronicDocumentsMonitor } from '../hooks/useElectronicDocumentsMonitor';
import { ElectronicDocumentsDashboardCards } from '../components/ElectronicDocumentsDashboardCards';
import { ElectronicDocumentsFilters } from '../components/ElectronicDocumentsFilters';
import { ElectronicDocumentsTable } from '../components/ElectronicDocumentsTable';
import { ElectronicDocumentDetailPanel } from '../components/ElectronicDocumentDetailPanel';

export function ElectronicDocumentsMonitorPage() {
  const { t } = useI18n();
  const m = useElectronicDocumentsMonitor();

  if (!m.canView) return <NoAccessPage title={t('electronicDocuments.monitor.title')} />;

  return (
    <ErpPageTemplate
      kicker={t('electronicDocuments.monitor.kicker')}
      title={t('electronicDocuments.monitor.title')}
      subtitle={t('electronicDocuments.monitor.subtitle')}
      action={
        <ZHBtn variant="secondary" type="button" disabled={m.listLoading || m.dashboardLoading} onClick={m.refresh}>
          <span className="material-symbols-outlined">refresh</span>
          {t('common.refresh')}
        </ZHBtn>
      }
    >
      {m.dashboardError && (
        <ZHPageNotice variant="error" message={t('electronicDocuments.monitor.dashboard.loadError')} detail={m.dashboardError} />
      )}
      <ElectronicDocumentsDashboardCards
        dashboard={m.dashboard}
        loading={m.dashboardLoading}
        activeState={m.filters.state}
        isTodayActive={m.isTodayFilterActive}
        onFilterState={m.toggleStateFilter}
        onFilterToday={m.toggleTodayFilter}
      />

      <div className="pg-section">
        <ElectronicDocumentsFilters
          dateFrom={m.filters.dateFrom}
          dateTo={m.filters.dateTo}
          state={m.filters.state}
          documentType={m.filters.documentType}
          environment={m.filters.environment}
          search={m.filters.search}
          onDateFromChange={(v) => { m.setDateFrom(v); m.setPage(1); }}
          onDateToChange={(v) => { m.setDateTo(v); m.setPage(1); }}
          onStateChange={(v) => { m.setState(v); m.setPage(1); }}
          onDocumentTypeChange={(v) => { m.setDocumentType(v); m.setPage(1); }}
          onEnvironmentChange={(v) => { m.setEnvironment(v); m.setPage(1); }}
          onSearchChange={(v) => { m.setSearch(v); m.setPage(1); }}
          onClear={m.resetFiltersAndReload}
          disabled={m.listLoading}
        />

        {m.listError && (
          <ZHPageNotice variant="error" message={t('electronicDocuments.monitor.table.loadError')} detail={m.listError} />
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
        retryLoading={m.retryLoading}
        onClose={m.closeDetail}
        onViewXml={(variant) => void m.viewXml(variant)}
        onRetryNow={m.retryNow}
      />
    </ErpPageTemplate>
  );
}
