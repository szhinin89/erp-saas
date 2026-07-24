import { ZHBtn } from '../ZHForm';
import { ZHPageNotice } from '../ZHPageNotice';
import { LoadingState } from '../../PageShell';
import { useI18n } from '../../../i18n/i18n';
import { downloadTextFile } from '../../../modules/electronicDocuments/monitor/utils/download';
import type { ElectronicDocumentXmlVariant } from './electronicDocumentDiagnosticTypes';

type Props = {
  xmlDraftAvailable: boolean;
  xmlSignedAvailable: boolean;
  xmlAuthorizedAvailable: boolean;
  xmlContent: string | null;
  xmlVariant: ElectronicDocumentXmlVariant | null;
  xmlLoading: boolean;
  xmlError: string | null;
  /** Nombre base para el archivo descargado (documentNumber, o el Id si no hay número). */
  downloadFileBaseName: string;
  onViewXml: (variant: ElectronicDocumentXmlVariant) => void;
};

/** Ver/descargar XML — nunca lo incrusta silenciosamente: solo muestra disponibilidad y, si se pide, el contenido ya cargado por el llamador. */
export function ElectronicDocumentXmlActions({
  xmlDraftAvailable, xmlSignedAvailable, xmlAuthorizedAvailable,
  xmlContent, xmlVariant, xmlLoading, xmlError, downloadFileBaseName, onViewXml,
}: Props) {
  const { t } = useI18n();

  return (
    <>
      <div className="edm-xml-actions">
        <ZHBtn variant="secondary" size="sm" type="button" disabled={!xmlDraftAvailable}
          onClick={() => onViewXml('Draft')}>
          {t('electronicDocuments.monitor.detail.viewXmlDraft')}
        </ZHBtn>
        <ZHBtn variant="secondary" size="sm" type="button" disabled={!xmlSignedAvailable}
          onClick={() => onViewXml('Signed')}>
          {t('electronicDocuments.monitor.detail.viewXmlSigned')}
        </ZHBtn>
        <ZHBtn variant="secondary" size="sm" type="button" disabled={!xmlAuthorizedAvailable}
          onClick={() => onViewXml('Authorized')}>
          {t('electronicDocuments.monitor.detail.viewXmlAuthorized')}
        </ZHBtn>
      </div>
      {!xmlDraftAvailable && !xmlSignedAvailable && !xmlAuthorizedAvailable && (
        <p className="edm-hint-sm">{t('electronicDocuments.monitor.detail.xmlNotAvailable')}</p>
      )}
      {xmlLoading && <LoadingState />}
      {xmlError && <ZHPageNotice variant="error" message={t('electronicDocuments.monitor.detail.xmlLoadError')} detail={xmlError} />}
      {xmlContent && xmlVariant && !xmlLoading && (
        <>
          <div className="edm-xml-viewer-header">
            <p className="edm-hint-sm">{t(`electronicDocuments.monitor.detail.viewXml${xmlVariant}`)}</p>
            <ZHBtn variant="secondary" size="sm" type="button"
              onClick={() => downloadTextFile(xmlContent, `${downloadFileBaseName}-${xmlVariant.toLowerCase()}.xml`)}>
              {t('electronicDocuments.monitor.detail.downloadXml')}
            </ZHBtn>
          </div>
          <pre className="edm-xml-viewer">{xmlContent}</pre>
        </>
      )}
    </>
  );
}
