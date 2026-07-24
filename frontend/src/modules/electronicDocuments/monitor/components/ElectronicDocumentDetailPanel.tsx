import { ZHDrawer } from '../../../../components/zh/ZHDrawer';
import { ZHCard } from '../../../../components/zh/ZHCard';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { LoadingState } from '../../../../components/PageShell';
import { ElectronicDocumentDiagnosticPanel } from '../../../../components/zh/electronicDocuments/ElectronicDocumentDiagnosticPanel';
import { useI18n } from '../../../../i18n/i18n';
import { message } from '../../../../lib/messages';
import type {
  ElectronicDocumentDetailDto,
  ElectronicDocumentXmlVariant,
} from '../api/electronicDocumentsMonitorService';
import './electronic-documents-monitor.css';

const RETRYABLE_STATES = new Set(['Signed', 'Received', 'DeadLetter', 'Failed']);

type Props = {
  open: boolean;
  detail: ElectronicDocumentDetailDto | null;
  loading: boolean;
  error: string | null;
  xmlContent: string | null;
  xmlVariant: ElectronicDocumentXmlVariant | null;
  xmlLoading: boolean;
  xmlError: string | null;
  canRetry: boolean;
  retryLoading: boolean;
  onClose: () => void;
  onViewXml: (variant: ElectronicDocumentXmlVariant) => void;
  onRetryNow: () => Promise<void>;
};

export function ElectronicDocumentDetailPanel({
  open, detail, loading, error,
  xmlContent, xmlVariant, xmlLoading, xmlError,
  canRetry, retryLoading,
  onClose, onViewXml, onRetryNow,
}: Props) {
  const { t } = useI18n();

  const handleRetryNow = async () => {
    const ok = await message.confirm({
      title: t('electronicDocuments.monitor.detail.retryConfirmTitle'),
      message: t('electronicDocuments.monitor.detail.retryConfirmMessage'),
      confirmLabel: t('electronicDocuments.monitor.detail.retryNow'),
    });
    if (!ok) return;
    try {
      await onRetryNow();
      message.success(t('electronicDocuments.monitor.detail.retrySuccess'));
    } catch {
      message.error(t('electronicDocuments.monitor.detail.retryError'));
    }
  };

  return (
    <ZHDrawer
      open={open}
      onClose={onClose}
      size="lg"
      title={t('electronicDocuments.monitor.detail.title')}
      subtitle={detail?.documentNumber ?? undefined}
    >
      {loading && <div className="pg-pad-40"><LoadingState /></div>}
      {error && <ZHPageNotice variant="error" message={t('electronicDocuments.monitor.detail.loadError')} detail={error} />}

      {detail && !loading && (
        <>
          <ZHCard title={t('electronicDocuments.monitor.detail.documentInfo')}>
            <div className="edm-detail-grid">
              <div>
                <div className="edm-detail-item-label">{t('electronicDocuments.monitor.detail.company')}</div>
                <div className="edm-detail-item-value">{detail.companyName}</div>
              </div>
              <div>
                <div className="edm-detail-item-label">{t('electronicDocuments.monitor.detail.companyTaxId')}</div>
                <div className="edm-detail-item-value">{detail.companyTaxId}</div>
              </div>
              <div>
                <div className="edm-detail-item-label">{t('electronicDocuments.monitor.detail.counterparty')}</div>
                <div className="edm-detail-item-value">{detail.counterpartyName ?? '—'}</div>
              </div>
              <div>
                <div className="edm-detail-item-label">{t('electronicDocuments.monitor.detail.documentNumber')}</div>
                <div className="edm-detail-item-value">{detail.documentNumber ?? '—'}</div>
              </div>
              <div>
                <div className="edm-detail-item-label">{t('electronicDocuments.monitor.detail.observations')}</div>
                <div className="edm-detail-item-value">
                  {detail.observations ?? t('electronicDocuments.monitor.detail.noObservations')}
                </div>
              </div>
            </div>
          </ZHCard>

          <ElectronicDocumentDiagnosticPanel
            diagnostic={detail.diagnostic}
            xmlContent={xmlContent}
            xmlVariant={xmlVariant}
            xmlLoading={xmlLoading}
            xmlError={xmlError}
            downloadFileBaseName={detail.documentNumber ?? detail.id}
            onViewXml={onViewXml}
            statusExtra={
              canRetry && RETRYABLE_STATES.has(detail.diagnostic.currentState) && (
                <ZHBtn variant="primary" size="sm" type="button"
                  disabled={retryLoading}
                  onClick={() => void handleRetryNow()}
                  className="edm-retry-btn">
                  {t('electronicDocuments.monitor.detail.retryNow')}
                </ZHBtn>
              )
            }
          />

          {detail.diagnostic.currentState === 'Authorized' && (
            <ZHCard title={t('electronicDocuments.monitor.detail.actionsSection')}>
              <div className="edm-xml-actions">
                <ZHBtn variant="secondary" size="sm" type="button" disabled
                  title={t('electronicDocuments.monitor.detail.comingSoon')}>
                  <span className="material-symbols-outlined zh-icon-sm">picture_as_pdf</span>
                  {t('electronicDocuments.monitor.detail.downloadRide')}
                </ZHBtn>
                <ZHBtn variant="secondary" size="sm" type="button" disabled
                  title={t('electronicDocuments.monitor.detail.comingSoon')}>
                  <span className="material-symbols-outlined zh-icon-sm">mail</span>
                  {t('electronicDocuments.monitor.detail.resendEmail')}
                </ZHBtn>
              </div>
              <p className="edm-hint-sm">{t('electronicDocuments.monitor.detail.comingSoon')}</p>
            </ZHCard>
          )}
        </>
      )}
    </ZHDrawer>
  );
}
