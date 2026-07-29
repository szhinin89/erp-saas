import type { ReactNode } from "react";
import { ZHCard } from "../ZHCard";
import { useI18n } from "../../../i18n/i18n";
import { ElectronicDocumentStatusBadge } from "./ElectronicDocumentStatusBadge";
import { ElectronicDocumentSriMessages } from "./ElectronicDocumentSriMessages";
import { ElectronicDocumentTimeline } from "./ElectronicDocumentTimeline";
import { ElectronicDocumentTechnicalInfo } from "./ElectronicDocumentTechnicalInfo";
import { ElectronicDocumentXmlActions } from "./ElectronicDocumentXmlActions";
import type {
  ElectronicDocumentDiagnosticDto,
  ElectronicDocumentXmlVariant,
} from "./electronicDocumentDiagnosticTypes";

type Props = {
  diagnostic: ElectronicDocumentDiagnosticDto;
  xmlContent: string | null;
  xmlVariant: ElectronicDocumentXmlVariant | null;
  xmlLoading: boolean;
  xmlError: string | null;
  /** Nombre base para el archivo XML descargado (p.ej. número de documento, o el Id si no hay número). */
  downloadFileBaseName: string;
  onViewXml: (variant: ElectronicDocumentXmlVariant) => void;
  /** Contenido adicional a mostrar junto al badge de estado (p.ej. botón "Reintentar ahora" del Monitor). */
  statusExtra?: ReactNode;
};

/**
 * Componente único y reutilizable para visualizar el resultado completo de las comunicaciones
 * con el SRI — estado, mensajes SRI reales, timeline, información técnica y disponibilidad de
 * XML. Cualquier pantalla del ERP lo embebe con una única propiedad (`diagnostic`), obtenida vía
 * `electronicDocumentDiagnosticService.getDiagnostic(id)` (Monitor) o
 * `.getDiagnosticBySource(sourceModule, sourceEntityId)` (cualquier otro módulo). No conoce nada
 * del módulo que lo usa — ni Ventas, ni Monitor, ni Retenciones.
 */
export function ElectronicDocumentDiagnosticPanel({
  diagnostic,
  xmlContent,
  xmlVariant,
  xmlLoading,
  xmlError,
  downloadFileBaseName,
  onViewXml,
  statusExtra,
}: Props) {
  const { t } = useI18n();

  return (
    <>
      <ZHCard title={t("electronicDocuments.monitor.detail.state")}>
        <div className="edm-state-badge-label">
          <ElectronicDocumentStatusBadge
            currentState={diagnostic.currentState}
          />
          {statusExtra}
        </div>
      </ZHCard>

      <ZHCard title={t("electronicDocuments.diagnostic.messagesSection")}>
        <ElectronicDocumentSriMessages messages={diagnostic.messages} />
      </ZHCard>

      <ZHCard title={t("electronicDocuments.monitor.detail.xmlSection")}>
        <ElectronicDocumentXmlActions
          xmlDraftAvailable={diagnostic.xmlDraftAvailable}
          xmlSignedAvailable={diagnostic.xmlSignedAvailable}
          xmlAuthorizedAvailable={diagnostic.xmlAuthorizedAvailable}
          xmlContent={xmlContent}
          xmlVariant={xmlVariant}
          xmlLoading={xmlLoading}
          xmlError={xmlError}
          downloadFileBaseName={downloadFileBaseName}
          onViewXml={onViewXml}
        />
      </ZHCard>

      <ZHCard title={t("electronicDocuments.monitor.detail.timeline")}>
        <ElectronicDocumentTimeline timeline={diagnostic.timeline} />
      </ZHCard>

      <ZHCard>
        <ElectronicDocumentTechnicalInfo
          technicalInfo={diagnostic.technicalInfo}
        />
      </ZHCard>
    </>
  );
}
