import { useState } from "react";
import { ZHDrawer } from "../../../../components/zh/ZHDrawer";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { LoadingState } from "../../../../components/PageShell";
import { ElectronicDocumentOperationalSheet } from "./ElectronicDocumentOperationalSheet";
import { useI18n } from "../../../../i18n/i18n";
import { message } from "../../../../lib/messages";
import type {
  ElectronicDocumentDetailDto,
  ElectronicDocumentXmlVariant,
} from "../api/electronicDocumentsMonitorService";
import { ReconcileElectronicDocumentModal } from "./ReconcileElectronicDocumentModal";
import { RetransmitCorrectedElectronicDocumentModal } from "./RetransmitCorrectedElectronicDocumentModal";
import "./electronic-documents-monitor.css";
import "../../../../components/zh/electronicDocuments/electronic-documents.css";

// ADR-036: "Dispatching" es reintentable solo como CONSULTA de autorización (el backend nunca
// reenvía); "Discarded" es terminal y no aparece aquí.
const RETRYABLE_STATES = new Set([
  "Signed",
  "Dispatching",
  "Received",
  "DeadLetter",
  "Failed",
]);

// El pipeline RIDE genérico (GET /api/v1/ride) no registra parser para Retención — su PDF solo
// existe como vista previa regenerada desde el origen, que no es el RIDE autorizado. Nunca se
// llama al endpoint genérico para este tipo (evita un "Failed/parser_not_registered" artificial).
const RIDE_UNSUPPORTED_DOCUMENT_TYPES = new Set(["Retention"]);

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
  canReconcile?: boolean;
  canRetransmitCorrected?: boolean;
  retryLoading: boolean;
  /** SRI-TOGGLE-06 — emisión de la empresa pausada: reintentar, conciliar y retransmitir llaman al SRI. */
  sriPaused?: boolean;
  canDownloadRide: boolean;
  rideDownloading: boolean;
  onClose: () => void;
  onViewXml: (variant: ElectronicDocumentXmlVariant) => void;
  onRetryNow: () => Promise<void>;
  onReconcile?: (values: {
    reference: string;
    observation: string;
  }) => Promise<void>;
  onRetransmitCorrected?: (values: {
    reason: string;
    idempotencyKey: string;
  }) => Promise<void>;
  onDownloadRide: () => void;
};

export function ElectronicDocumentDetailPanel({
  open,
  detail,
  loading,
  error,
  xmlContent,
  xmlVariant,
  xmlLoading,
  xmlError,
  canRetry,
  canReconcile = false,
  canRetransmitCorrected = false,
  retryLoading,
  sriPaused = false,
  canDownloadRide,
  rideDownloading,
  onClose,
  onViewXml,
  onRetryNow,
  onReconcile,
  onRetransmitCorrected,
  onDownloadRide,
}: Props) {
  const { t } = useI18n();
  const [reconcileOpen, setReconcileOpen] = useState(false);
  const [retransmitOpen, setRetransmitOpen] = useState(false);
  // R12 Fase B — la elegibilidad y sus motivos los calcula el backend; la UI solo los muestra.
  const retransmission = detail?.diagnostic.retransmission ?? null;
  const showRetransmission =
    canRetransmitCorrected &&
    onRetransmitCorrected !== undefined &&
    detail?.diagnostic.currentState === "Rejected" &&
    retransmission !== null;
  const rideUnsupported =
    detail !== null &&
    RIDE_UNSUPPORTED_DOCUMENT_TYPES.has(detail.documentType);

  const handleRetryNow = async () => {
    const ok = await message.confirm({
      title: t("electronicDocuments.monitor.detail.retryConfirmTitle"),
      message: t("electronicDocuments.monitor.detail.retryConfirmMessage"),
      confirmLabel: t("electronicDocuments.monitor.detail.retryNow"),
    });
    if (!ok) return;
    try {
      await onRetryNow();
      message.success(t("electronicDocuments.monitor.detail.retrySuccess"));
    } catch {
      message.error(t("electronicDocuments.monitor.detail.retryError"));
    }
  };

  return (
    <ZHDrawer
      open={open}
      onClose={onClose}
      size="lg"
      title={t("electronicDocuments.monitor.detail.title")}
      subtitle={
        detail
          ? `${t(`electronicDocuments.monitor.documentType.${detail.documentType}`)} · ${detail.documentNumber ?? detail.id}`
          : undefined
      }
    >
      {loading && (
        <div className="pg-pad-40">
          <LoadingState />
        </div>
      )}
      {error && (
        <ZHPageNotice
          variant="error"
          message={t("electronicDocuments.monitor.detail.loadError")}
          detail={error}
        />
      )}

      {detail && !loading && (
        <ElectronicDocumentOperationalSheet
          key={detail.id}
          detail={detail}
          xmlContent={xmlContent}
          xmlVariant={xmlVariant}
          xmlLoading={xmlLoading}
          xmlError={xmlError}
          onViewXml={onViewXml}
          actions={
            <div className="zh-form-actions-row">
              {canRetry &&
                RETRYABLE_STATES.has(detail.diagnostic.currentState) && (
                  <ZHBtn
                    variant="primary"
                    size="sm"
                    type="button"
                    disabled={retryLoading || sriPaused}
                    title={sriPaused ? t("electronicDocuments.monitor.sriPaused.actionHint") : undefined}
                    onClick={() => void handleRetryNow()}
                  >
                    {t("electronicDocuments.monitor.detail.retryNow")}
                  </ZHBtn>
                )}
              {canReconcile &&
                onReconcile &&
                detail.diagnostic.requiresReconciliation && (
                  <ZHBtn
                    variant="secondary"
                    size="sm"
                    type="button"
                    disabled={retryLoading || sriPaused}
                    title={sriPaused ? t("electronicDocuments.monitor.sriPaused.actionHint") : undefined}
                    onClick={() => setReconcileOpen(true)}
                  >
                    {t("electronicDocuments.monitor.reconcile.open")}
                  </ZHBtn>
                )}
              {showRetransmission && retransmission.eligible && (
                <ZHBtn
                  variant="primary"
                  size="sm"
                  type="button"
                  disabled={retryLoading || sriPaused}
                  title={sriPaused ? t("electronicDocuments.monitor.sriPaused.actionHint") : undefined}
                  onClick={() => setRetransmitOpen(true)}
                >
                  {t("electronicDocuments.monitor.retransmit.open")}
                </ZHBtn>
              )}
            </div>
          }
          retransmissionBlocks={
            showRetransmission && !retransmission.eligible ? (
              <>
                <p className="zh-summary-grid__label">
                  {t("electronicDocuments.monitor.retransmit.blockedTitle")}
                </p>
                <ul
                  className="zh-edoc-hint-sm"
                  aria-label={t("electronicDocuments.monitor.retransmit.blockedTitle")}
                >
                  {retransmission.blocks.map((block) => (
                    <li key={block.code}>{block.message}</li>
                  ))}
                </ul>
              </>
            ) : null
          }
          authorizedFiles={
            detail.diagnostic.currentState === "Authorized" ? (
              <>
                <div className="zh-edoc-xml-actions">
                  {canDownloadRide && (
                    <ZHBtn
                      variant="secondary"
                      size="sm"
                      type="button"
                      disabled={rideUnsupported || rideDownloading}
                      onClick={onDownloadRide}
                    >
                      <span className="material-symbols-outlined zh-icon-sm">
                        picture_as_pdf
                      </span>
                      {t("electronicDocuments.monitor.detail.downloadRide")}
                    </ZHBtn>
                  )}
                  <ZHBtn
                    variant="secondary"
                    size="sm"
                    type="button"
                    disabled
                    title={t("electronicDocuments.monitor.detail.comingSoon")}
                  >
                    <span className="material-symbols-outlined zh-icon-sm">
                      mail
                    </span>
                    {t("electronicDocuments.monitor.detail.resendEmail")}
                  </ZHBtn>
                </div>
                {canDownloadRide && rideUnsupported && (
                  <p className="zh-edoc-hint-sm">
                    {t("electronicDocuments.monitor.detail.rideRetentionUnavailable")}
                  </p>
                )}
                <p className="zh-edoc-hint-sm">
                  {t("electronicDocuments.monitor.detail.resendEmailComingSoon")}
                </p>
              </>
            ) : (
              <p className="zh-edoc-hint-sm">
                {t("electronicDocuments.monitor.sheet.rideOnlyAuthorized")}
              </p>
            )
          }
        />
      )}
      {retransmitOpen && onRetransmitCorrected && retransmission && (
        <RetransmitCorrectedElectronicDocumentModal
          retransmission={retransmission}
          onClose={() => setRetransmitOpen(false)}
          onSubmit={onRetransmitCorrected}
        />
      )}
      {reconcileOpen && onReconcile && (
        <ReconcileElectronicDocumentModal
          onClose={() => setReconcileOpen(false)}
          onSubmit={onReconcile}
        />
      )}
    </ZHDrawer>
  );
}
