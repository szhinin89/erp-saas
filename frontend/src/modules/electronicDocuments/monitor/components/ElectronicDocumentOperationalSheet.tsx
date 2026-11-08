import { useId, useState, type ReactNode } from "react";
import { Badge } from "../../../../components/PageShell";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHIconButton } from "../../../../components/zh/ZHIconButton";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ElectronicDocumentStatusBadge } from "../../../../components/zh/electronicDocuments/ElectronicDocumentStatusBadge";
import { ElectronicDocumentSriMessages } from "../../../../components/zh/electronicDocuments/ElectronicDocumentSriMessages";
import { ElectronicDocumentTimeline } from "../../../../components/zh/electronicDocuments/ElectronicDocumentTimeline";
import { ElectronicDocumentXmlActions } from "../../../../components/zh/electronicDocuments/ElectronicDocumentXmlActions";
import { useI18n } from "../../../../i18n/i18n";
import { copyToClipboard } from "../../../../lib/clipboard";
import { formatDateTime } from "../../../../lib/formatters/dateFormatters";
import { message } from "../../../../lib/messages";
import type {
  ElectronicDocumentDetailDto,
  ElectronicDocumentXmlVariant,
} from "../api/electronicDocumentsMonitorService";
import {
  incidenceVariant,
  isLongResponse,
  latestEvent,
  latestMessage,
  sriAuthorizationStatus,
  sriReceptionStatus,
  stateMeaningKey,
} from "../utils/sriSituation";
import "./electronic-documents-monitor.css";
import "../../../../components/zh/electronicDocuments/electronic-documents.css";

type Props = {
  detail: ElectronicDocumentDetailDto;
  xmlContent: string | null;
  xmlVariant: ElectronicDocumentXmlVariant | null;
  xmlLoading: boolean;
  xmlError: string | null;
  onViewXml: (variant: ElectronicDocumentXmlVariant) => void;
  /** Acciones operativas existentes (reintentar, conciliar, retransmitir): las decide el panel. */
  actions: ReactNode;
  /** Motivos de bloqueo de la retransmisión, cuando el backend los informa. */
  retransmissionBlocks: ReactNode;
  /** RIDE y correo (solo Autorizado), con sus permisos y avisos existentes. */
  authorizedFiles: ReactNode;
};

type Section = "messages" | "history" | "support";

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <span className="zh-summary-grid__label">{label}</span>
      <span className="zh-summary-grid__value">{children}</span>
    </div>
  );
}

/**
 * SRI-MONITOR-SHEET-01 — ficha operativa del Monitor: situación ante el SRI, comprobante y
 * autorización, última respuesta, fechas, archivos y acciones a la vista; historial, todos los
 * mensajes y datos de soporte plegados (`ZHBtn expanded`). Solo compone piezas oficiales de
 * `components/zh/electronicDocuments`; Ventas y Notas de crédito siguen usando
 * `ElectronicDocumentDiagnosticPanel` sin cambios.
 */
export function ElectronicDocumentOperationalSheet({
  detail,
  xmlContent,
  xmlVariant,
  xmlLoading,
  xmlError,
  onViewXml,
  actions,
  retransmissionBlocks,
  authorizedFiles,
}: Props) {
  const { t } = useI18n();
  const ids = useId();
  const [open, setOpen] = useState<Record<Section, boolean>>({
    messages: false,
    history: false,
    support: false,
  });
  const [responseExpanded, setResponseExpanded] = useState(false);
  const toggle = (s: Section) => setOpen((o) => ({ ...o, [s]: !o[s] }));

  const d = detail.diagnostic;
  const info = d.technicalInfo;
  const na = t("electronicDocuments.monitor.sheet.notAvailable");
  const date = (v: string | null | undefined) => (v ? formatDateTime(v) : na);

  const environment =
    d.environment === "2" ? "prod" : d.environment === "1" ? "test" : null;
  const reception = sriReceptionStatus(d);
  const receivedEvent = latestEvent(d.timeline, "Received");
  const sentEvent = latestEvent(d.timeline, "Sent");
  const authorization = sriAuthorizationStatus(d);
  const incidence = incidenceVariant(d.currentState);
  const latest = latestMessage(d.messages);
  const longResponse = latest !== null && isLongResponse(latest);
  const reconciliationEvidence = d.requiresReconciliation
    ? latestEvent(d.timeline, "ReconciliationRecorded")
    : null;

  const copyAccessKey = async (value: string) => {
    const ok = await copyToClipboard(value);
    if (ok) message.success(t("electronicDocuments.monitor.table.accessKeyCopied"));
    else message.error(t("electronicDocuments.monitor.table.accessKeyCopyFailed"));
  };

  const sectionId = (s: Section) => `${ids}-${s}`;

  return (
    <>
      <ZHCard className="zh-mb-8" title={t("electronicDocuments.monitor.sheet.situation")}>
        {/* Badges en línea (no en una fila de acciones: en móvil esa fila apila y estira). */}
        <div className="zh-mb-8">
          <ElectronicDocumentStatusBadge currentState={d.currentState} />{" "}
          {environment && (
            <Badge
              size="md"
              variant={environment === "prod" ? "info" : "warning"}
              label={t(
                environment === "prod"
                  ? "settings.electronicInvoicing.sri.environmentProd"
                  : "settings.electronicInvoicing.sri.environmentTest",
              )}
            />
          )}
        </div>
        {incidence ? (
          <ZHPageNotice
            variant={incidence}
            message={t(stateMeaningKey(d))}
          />
        ) : (
          <p className="zh-readonly-text">{t(stateMeaningKey(d))}</p>
        )}
        {d.requiresReconciliation && (
          <ZHPageNotice
            variant="warning"
            message={t("electronicDocuments.monitor.reconcile.pendingTitle")}
            detail={t("electronicDocuments.monitor.reconcile.pendingGuidance")}
          />
        )}
        {reconciliationEvidence?.reason && (
          <p className="zh-readonly-text">
            {t("electronicDocuments.monitor.sheet.lastEvidence", {
              date: formatDateTime(reconciliationEvidence.occurredAtUtc),
              user: reconciliationEvidence.userName,
            })}{" "}
            {reconciliationEvidence.reason}
          </p>
        )}
        {retransmissionBlocks}
        {actions}
      </ZHCard>

      <ZHCard className="zh-mb-8" title={t("electronicDocuments.monitor.sheet.document")}>
        <div className="zh-summary-grid zh-mb-16">
          <Field label={t("electronicDocuments.monitor.table.documentType")}>
            {t(`electronicDocuments.monitor.documentType.${detail.documentType}`)}
          </Field>
          <Field label={t("electronicDocuments.monitor.detail.documentNumber")}>
            {detail.documentNumber ?? na}
          </Field>
          <Field label={t("electronicDocuments.monitor.detail.company")}>
            {detail.companyName}
            <span className="zh-edoc-hint-sm"> · {detail.companyTaxId}</span>
          </Field>
          <Field label={t("electronicDocuments.monitor.detail.counterparty")}>
            {detail.counterpartyName ?? na}
          </Field>
          <Field label={t("electronicDocuments.monitor.sheet.reception")}>
            {t(`electronicDocuments.monitor.sheet.receptionStatus.${reception}`)}
            {reception === "received" && receivedEvent && (
              <span className="zh-edoc-hint-sm">
                {" "}
                · {formatDateTime(receivedEvent.occurredAtUtc)}
              </span>
            )}
          </Field>
          <Field label={t("electronicDocuments.monitor.sheet.authorization")}>
            {t(
              `electronicDocuments.monitor.sheet.authorizationStatus.${authorization}`,
            )}
          </Field>
          <Field label={t("electronicDocuments.monitor.detail.authorizationNumber")}>
            {info.authorizationNumber ? (
              <span className="zh-code-value">{info.authorizationNumber}</span>
            ) : (
              na
            )}
          </Field>
          <Field label={t("electronicDocuments.monitor.detail.authorizationDate")}>
            {date(info.authorizationDate)}
          </Field>
        </div>
        <div className="zh-summary-grid">
          <Field label={t("electronicDocuments.monitor.detail.accessKey")}>
            {info.accessKey ? (
              <span className="edm-access-key zh-code-value">
                {info.accessKey}
                <ZHIconButton
                  icon="content_copy"
                  title={t("electronicDocuments.monitor.table.copyAccessKey")}
                  onClick={() => void copyAccessKey(info.accessKey!)}
                />
              </span>
            ) : (
              t("electronicDocuments.monitor.sheet.accessKeyPending")
            )}
          </Field>
        </div>
      </ZHCard>

      <ZHCard className="zh-mb-8" title={t("electronicDocuments.monitor.sheet.latestResponse")}>
        {latest ? (
          <>
            <ElectronicDocumentSriMessages
              messages={[
                longResponse && !responseExpanded
                  ? { ...latest, additionalInfo: null }
                  : latest,
              ]}
            />
            {longResponse && latest.additionalInfo && (
              <ZHBtn
                type="button"
                variant="secondary"
                size="sm"
                expanded={responseExpanded}
                onClick={() => setResponseExpanded((v) => !v)}
              >
                {t("electronicDocuments.monitor.sheet.fullResponse")}
              </ZHBtn>
            )}
          </>
        ) : (
          <p className="zh-edoc-hint-sm">
            {t("electronicDocuments.monitor.sheet.noResponse")}
          </p>
        )}
      </ZHCard>

      <ZHCard className="zh-mb-8" title={t("electronicDocuments.monitor.sheet.datesAndAttempts")}>
        <div className="zh-summary-grid">
          <Field label={t("electronicDocuments.monitor.sheet.registeredAt")}>
            {date(detail.createdAt)}
          </Field>
          <Field label={t("electronicDocuments.monitor.sheet.lastSent")}>
            {date(sentEvent?.occurredAtUtc)}
          </Field>
          <Field label={t("electronicDocuments.monitor.detail.lastAttempt")}>
            {date(d.lastAttemptUtc)}
          </Field>
          <Field label={t("electronicDocuments.monitor.table.retries")}>
            {info.retryCount}
          </Field>
          <Field label={t("electronicDocuments.monitor.table.updatedAt")}>
            {date(detail.updatedAt)}
          </Field>
        </div>
      </ZHCard>

      <ZHCard className="zh-mb-8" title={t("electronicDocuments.monitor.sheet.files")}>
        <ElectronicDocumentXmlActions
          xmlDraftAvailable={d.xmlDraftAvailable}
          xmlSignedAvailable={d.xmlSignedAvailable}
          xmlAuthorizedAvailable={d.xmlAuthorizedAvailable}
          xmlContent={xmlContent}
          xmlVariant={xmlVariant}
          xmlLoading={xmlLoading}
          xmlError={xmlError}
          downloadFileBaseName={detail.documentNumber ?? detail.id}
          onViewXml={onViewXml}
        />
        {authorizedFiles}
      </ZHCard>

      <div className="zh-form-actions-row">
        {d.messages.length > 1 && (
          <ZHBtn
            type="button"
            variant="secondary"
            icon="forum"
            expanded={open.messages}
            aria-controls={sectionId("messages")}
            onClick={() => toggle("messages")}
          >
            {t("electronicDocuments.monitor.sheet.allMessages", {
              count: d.messages.length,
            })}
          </ZHBtn>
        )}
        <ZHBtn
          type="button"
          variant="secondary"
          icon="history"
          expanded={open.history}
          aria-controls={sectionId("history")}
          onClick={() => toggle("history")}
        >
          {t("electronicDocuments.monitor.sheet.history", {
            count: d.timeline.length,
          })}
        </ZHBtn>
        <ZHBtn
          type="button"
          variant="secondary"
          icon="support_agent"
          expanded={open.support}
          aria-controls={sectionId("support")}
          onClick={() => toggle("support")}
        >
          {t("electronicDocuments.monitor.sheet.support")}
        </ZHBtn>
      </div>

      {open.messages && d.messages.length > 1 && (
        <div id={sectionId("messages")}>
          <ZHCard className="zh-mb-8" title={t("electronicDocuments.diagnostic.messagesSection")}>
            <ElectronicDocumentSriMessages messages={d.messages} />
          </ZHCard>
        </div>
      )}
      {open.history && (
        <div id={sectionId("history")}>
          <ZHCard className="zh-mb-8" title={t("electronicDocuments.monitor.detail.timeline")}>
            <p className="zh-edoc-hint-sm">
              {t("electronicDocuments.monitor.sheet.historyScope")}
            </p>
            <ElectronicDocumentTimeline timeline={d.timeline} />
          </ZHCard>
        </div>
      )}
      {open.support && (
        <div id={sectionId("support")}>
          <ZHCard className="zh-mb-8" title={t("electronicDocuments.monitor.sheet.support")}>
            <div className="zh-summary-grid">
              <Field label={t("electronicDocuments.monitor.sheet.documentId")}>
                <span className="zh-code-value">{detail.id}</span>
              </Field>
              <Field label={t("electronicDocuments.monitor.sheet.sourceModule")}>
                {detail.sourceModule}
              </Field>
              <Field label={t("electronicDocuments.monitor.sheet.sourceEntityId")}>
                <span className="zh-code-value">{detail.sourceEntityId}</span>
              </Field>
              <Field
                label={t("electronicDocuments.diagnostic.technicalInfoCorrelationId")}
              >
                {info.correlationId ?? na}
              </Field>
              <Field label={t("electronicDocuments.monitor.detail.observations")}>
                {detail.observations ??
                  t("electronicDocuments.monitor.detail.noObservations")}
              </Field>
            </div>
          </ZHCard>
        </div>
      )}
    </>
  );
}
