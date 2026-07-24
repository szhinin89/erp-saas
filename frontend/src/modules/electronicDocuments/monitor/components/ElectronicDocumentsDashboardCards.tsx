import { ZHCard } from '../../../../components/zh/ZHCard';
import { ZHGrid } from '../../../../components/zh/ZHForm';
import { useI18n } from '../../../../i18n/i18n';
import type { ElectronicDocumentsDashboardDto } from '../api/electronicDocumentsMonitorService';
import './electronic-documents-monitor.css';

type Props = {
  dashboard: ElectronicDocumentsDashboardDto | null;
  loading: boolean;
  activeState: string;
  isTodayActive: boolean;
  onFilterState: (value: string) => void;
  onFilterToday: () => void;
};

const PENDING_STATES = 'Draft,XmlGenerated';
const IN_PROGRESS_STATES = 'Signed,Sent,Received';
const ERROR_STATES = 'DeadLetter,Failed';

function formatMinutes(minutes: number | null, naLabel: string): string {
  if (minutes === null) return naLabel;
  if (minutes < 60) return `${Math.round(minutes)} min`;
  return `${(minutes / 60).toFixed(1)} h`;
}

export function ElectronicDocumentsDashboardCards({
  dashboard, loading, activeState, isTodayActive, onFilterState, onFilterToday,
}: Props) {
  const { t } = useI18n();
  const naLabel = t('electronicDocuments.monitor.dashboard.notAvailable');

  const value = (n: number | undefined) => (loading || dashboard === null ? '—' : (n ?? 0).toString());

  const cardCls = (active: boolean) => `edm-kpi-card${active ? ' edm-kpi-card--active' : ''}`;

  return (
    <>
      <ZHGrid cols={3}>
        <ZHCard title={t('electronicDocuments.monitor.dashboard.pending')} className={cardCls(activeState === PENDING_STATES)}>
          <button type="button" className="edm-kpi-btn" onClick={() => onFilterState(PENDING_STATES)}>
            <div className="edm-metric">{value(dashboard?.pending)}</div>
          </button>
        </ZHCard>
        <ZHCard title={t('electronicDocuments.monitor.dashboard.inProgress')} className={cardCls(activeState === IN_PROGRESS_STATES)}>
          <button type="button" className="edm-kpi-btn" onClick={() => onFilterState(IN_PROGRESS_STATES)}>
            <div className="edm-metric">{value(dashboard ? dashboard.signed + dashboard.sent : undefined)}</div>
          </button>
        </ZHCard>
        <ZHCard title={t('electronicDocuments.monitor.dashboard.authorized')} className={cardCls(activeState === 'Authorized')}>
          <button type="button" className="edm-kpi-btn" onClick={() => onFilterState('Authorized')}>
            <div className="edm-metric edm-metric--success">{value(dashboard?.authorized)}</div>
          </button>
        </ZHCard>
      </ZHGrid>

      <ZHGrid cols={3}>
        <ZHCard title={t('electronicDocuments.monitor.dashboard.rejected')} className={cardCls(activeState === 'Rejected')}>
          <button type="button" className="edm-kpi-btn" onClick={() => onFilterState('Rejected')}>
            <div className="edm-metric edm-metric--danger">{value(dashboard?.rejected)}</div>
          </button>
        </ZHCard>
        <ZHCard title={t('electronicDocuments.monitor.dashboard.error')} className={cardCls(activeState === ERROR_STATES)}>
          <button type="button" className="edm-kpi-btn" onClick={() => onFilterState(ERROR_STATES)}>
            <div className="edm-metric edm-metric--danger">{value(dashboard?.error)}</div>
          </button>
        </ZHCard>
        <ZHCard title={t('electronicDocuments.monitor.dashboard.totalToday')} className={cardCls(isTodayActive)}>
          <button type="button" className="edm-kpi-btn" onClick={onFilterToday}>
            <div className="edm-metric">{value(dashboard?.totalToday)}</div>
          </button>
        </ZHCard>
      </ZHGrid>

      <ZHGrid cols={3}>
        <ZHCard title={t('electronicDocuments.monitor.dashboard.authorizedToday')}>
          <div className="edm-metric edm-metric--success">{value(dashboard?.authorizedToday)}</div>
        </ZHCard>
        <ZHCard title={t('electronicDocuments.monitor.dashboard.errorsToday')}>
          <div className="edm-metric edm-metric--danger">{value(dashboard?.errorsToday)}</div>
        </ZHCard>
        <ZHCard title={t('electronicDocuments.monitor.dashboard.pendingRetries')}>
          <div className="edm-metric">{value(dashboard?.pendingRetries)}</div>
        </ZHCard>
      </ZHGrid>

      <ZHGrid cols={3}>
        <ZHCard title={t('electronicDocuments.monitor.dashboard.averageAuthorizationTime')}>
          <div className="edm-metric">
            {loading || dashboard === null ? '—' : formatMinutes(dashboard.averageAuthorizationMinutes, naLabel)}
          </div>
        </ZHCard>
      </ZHGrid>
    </>
  );
}
