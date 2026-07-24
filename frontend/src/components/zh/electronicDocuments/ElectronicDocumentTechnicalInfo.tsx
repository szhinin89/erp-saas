import { useI18n } from '../../../i18n/i18n';
import { message } from '../../../lib/messages';
import { formatDateTime } from '../../../lib/formatters/dateFormatters';
import { copyToClipboard } from '../../../modules/electronicDocuments/monitor/utils/clipboard';
import type { ElectronicDocumentTechnicalInfoDto } from './electronicDocumentDiagnosticTypes';

type Props = { technicalInfo: ElectronicDocumentTechnicalInfoDto };

function CopyableValue({ value }: { value: string }) {
  const { t } = useI18n();
  const handleCopy = async () => {
    const ok = await copyToClipboard(value);
    if (ok) message.success(t('electronicDocuments.diagnostic.valueCopied'));
    else message.error(t('electronicDocuments.diagnostic.valueCopyFailed'));
  };
  return (
    <span className="edm-tech-info-value">
      {value}
      <button type="button" className="edm-icon-btn" onClick={() => void handleCopy()}
        aria-label={t('electronicDocuments.diagnostic.copyValue')}>
        <span className="material-symbols-outlined zh-icon-sm">content_copy</span>
      </button>
    </span>
  );
}

/** Sección colapsable "solo para soporte" — nunca se usa para decisiones de negocio del módulo consumidor. */
export function ElectronicDocumentTechnicalInfo({ technicalInfo }: Props) {
  const { t } = useI18n();
  const na = t('electronicDocuments.diagnostic.technicalInfoNotAvailable');

  const rows: Array<[string, string | null]> = [
    [t('electronicDocuments.diagnostic.technicalInfoAccessKey'), technicalInfo.accessKey],
    [t('electronicDocuments.diagnostic.technicalInfoAuthorizationNumber'), technicalInfo.authorizationNumber],
    [t('electronicDocuments.diagnostic.technicalInfoEnvironment'), technicalInfo.environment],
    [t('electronicDocuments.diagnostic.technicalInfoAuthorizationDate'),
      technicalInfo.authorizationDate ? formatDateTime(technicalInfo.authorizationDate) : null],
    [t('electronicDocuments.diagnostic.technicalInfoRetryCount'), String(technicalInfo.retryCount)],
    [t('electronicDocuments.diagnostic.technicalInfoLastAttempt'),
      technicalInfo.lastAttemptUtc ? formatDateTime(technicalInfo.lastAttemptUtc) : null],
    [t('electronicDocuments.diagnostic.technicalInfoCorrelationId'), technicalInfo.correlationId],
  ];

  return (
    <details className="edm-tech-info">
      <summary>{t('electronicDocuments.diagnostic.technicalInfoSection')}</summary>
      <div className="edm-tech-info-grid">
        {rows.map(([label, value]) => (
          <div key={label}>
            <div className="edm-detail-item-label">{label}</div>
            <div className="edm-detail-item-value">
              {value ? <CopyableValue value={value} /> : na}
            </div>
          </div>
        ))}
      </div>
    </details>
  );
}
