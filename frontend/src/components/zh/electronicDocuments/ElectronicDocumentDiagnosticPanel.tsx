import type { ReactNode } from "react";
import { Badge } from "../../PageShell";
import { ZHCard } from "../ZHCard";
import { useI18n } from "../../../i18n/i18n";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
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

function availableXmlCount(diagnostic: ElectronicDocumentDiagnosticDto): number {
  return [
    diagnostic.xmlDraftAvailable,
    diagnostic.xmlSignedAvailable,
    diagnostic.xmlAuthorizedAvailable,
  ].filter(Boolean).length;
}

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
  const xmlAvailableCount = availableXmlCount(diagnostic);
  const sriMessagesCount = diagnostic.messages.length;

  return (
    <>
      <ZHCard
        title={t(
          "electronicDocuments.diagnostic.summarySection",
          "Resumen del diagnóstico",
        )}
      >
        <div className="edm-diagnostic-summary">
          <div className="edm-diagnostic-summary__state">
            <ElectronicDocumentStatusBadge
              currentState={diagnostic.currentState}
            />
            {statusExtra}
          </div>

          <div className="edm-diagnostic-summary__meta">
            <div>
              <div className="edm-detail-item-label">
                {t(
                  "electronicDocuments.diagnostic.summaryEnvironment",
                  "Ambiente",
                )}
              </div>
              <div className="edm-detail-item-value">
                {diagnostic.environment ?? "—"}
              </div>
            </div>
            <div>
              <div className="edm-detail-item-label">
                {t(
                  "electronicDocuments.diagnostic.summaryLastAttempt",
                  "Último intento",
                )}
              </div>
              <div className="edm-detail-item-value">
                {diagnostic.lastAttemptUtc
                  ? formatDateTime(diagnostic.lastAttemptUtc)
                  : "—"}
              </div>
            </div>
            <div>
              <div className="edm-detail-item-label">
                {t("electronicDocuments.diagnostic.messagesSection")}
              </div>
              <div className="edm-detail-item-value">
                <Badge
                  variant={sriMessagesCount > 0 ? "warning" : "neutral"}
                  label={String(sriMessagesCount)}
                  size="md"
                />
              </div>
            </div>
            <div>
              <div className="edm-detail-item-label">
                {t("electronicDocuments.monitor.detail.xmlSection")}
              </div>
              <div className="edm-detail-item-value">
                <Badge
                  variant={xmlAvailableCount > 0 ? "info" : "neutral"}
                  label={t(
                    "electronicDocuments.diagnostic.summaryXmlAvailable",
                    "{{count}} disponibles",
                  ).replace("{{count}}", String(xmlAvailableCount))}
                  size="md"
                />
              </div>
            </div>
          </div>
        </div>
      </ZHCard>

      <ZHCard title={t("electronicDocuments.diagnostic.messagesSection")}>
        <ElectronicDocumentSriMessages messages={diagnostic.messages} />
      </ZHCard>

      <div className="edm-diagnostic-secondary">
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
      </div>

      <ZHCard
        title={t(
          "electronicDocuments.diagnostic.supportSection",
          "Soporte técnico",
        )}
      >
        <ElectronicDocumentTechnicalInfo
          technicalInfo={diagnostic.technicalInfo}
        />
      </ZHCard>
    </>
  );
}
