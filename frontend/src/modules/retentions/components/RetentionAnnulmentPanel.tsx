import { useState } from "react";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { copyToClipboard } from "../../../lib/clipboard";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import {
  retentionsService,
  type RetentionAnnulmentRequestDto,
  type RetentionAnnulmentStatus,
} from "../api/retentionsService";
import { AbandonAnnulmentModal, SubmitAnnulmentModal } from "./RetentionAnnulmentModals";

/**
 * ZH-RETENTION-SRI-ANNULMENT-01/01B — situación de la anulación ante el SRI de una retención AUTORIZADA,
 * dentro del detalle de la Compra/Gasto (no hay pantalla paralela). La SOLICITUD es asistida: el usuario
 * la presenta en SRI en Línea con los datos que se muestran aquí (copiables) e indica "Ya presenté la
 * solicitud". La VERIFICACIÓN es automática: el backend consulta ConsultaComprobante (al registrar la
 * presentación, a demanda y periódicamente) y solo un ANULADO informado por el SRI anula el documento. El
 * usuario nunca declara el estado fiscal. Nunca se muestra "Anulado" antes de que el SRI lo informe y el
 * ERP lo finalice.
 */
export type RetentionAnnulmentPanelProps = {
  annulment: RetentionAnnulmentRequestDto;
  /** Documento origen de la retención (define los textos: "la compra" / "el gasto"). */
  origin: RetentionAnnulmentOrigin;
  /** Permiso de anular el origen (presentar, verificar en el SRI, completar, desistir). */
  canOperate: boolean;
  onChanged: (updated: RetentionAnnulmentRequestDto) => void;
};

export type RetentionAnnulmentOrigin = "purchase" | "expense";

/** Textos por origen (concordancia de género: "la compra … anulada" / "el gasto … anulado"). */
const ANNULMENT_ORIGIN_TEXT: Record<
  RetentionAnnulmentOrigin,
  { the: string; notCancelled: string; cancelled: string; inForce: string; complete: string; finalizedLabel: string }
> = {
  purchase: {
    the: "la compra",
    notCancelled: "La compra aún NO está anulada.",
    cancelled: "El SRI confirmó la anulación y la compra quedó anulada.",
    inForce: "Compra vigente: la solicitud no prosperó y la retención continúa autorizada.",
    complete: "Completar anulación de la compra",
    finalizedLabel: "Compra anulada",
  },
  expense: {
    the: "el gasto",
    notCancelled: "El gasto aún NO está anulado.",
    cancelled: "El SRI confirmó la anulación y el gasto quedó anulado.",
    inForce: "Gasto vigente: la solicitud no prosperó y la retención continúa autorizada.",
    complete: "Completar anulación del gasto",
    finalizedLabel: "Gasto anulado",
  },
};

/** Mensajes exactos del ticket 01B por lo que informa la última consulta al SRI. */
const SRI_STILL_AUTHORIZED = "El SRI todavía mantiene vigente el comprobante";
const SRI_PENDING_ANNULMENT = "Pendiente de anulación en SRI";
const SRI_NOT_AUTHORIZED = "El SRI informa NO AUTORIZADO para el comprobante. No se realizó ningún cambio; requiere revisión.";
const SRI_VERIFICATION_FAILED = "No fue posible verificar el estado en SRI";

function statusPresentation(
  a: RetentionAnnulmentRequestDto,
): { label: string; variant: BadgeVariant } {
  const map: Record<RetentionAnnulmentStatus, { label: string; variant: BadgeVariant }> = {
    PendingSubmission: { label: "Pendiente de presentar al SRI", variant: "warning" },
    PendingSriResolution:
      a.lastSriQueryOutcome === "Success" && a.lastSriFiscalStatus === "PendingAnnulment"
        ? { label: SRI_PENDING_ANNULMENT, variant: "info" }
        : { label: "Presentada al SRI — en verificación", variant: "info" },
    Accepted: a.finalizedAtUtc
      ? { label: "Anulada por el SRI", variant: "success" }
      : { label: "Anulada por el SRI — anulación del documento pendiente", variant: "warning" },
    // Reservados (sin transición desde 01B); se presentan si existen datos previos.
    Rejected: { label: "Rechazada por el SRI", variant: "error" },
    Expired: { label: "Solicitud sin efecto", variant: "neutral" },
    Abandoned: { label: "Desistida", variant: "neutral" },
  };
  return map[a.status];
}

/** Lo que informó la última consulta a ConsultaComprobante (nunca lo declara el usuario). */
function lastSriCheckText(a: RetentionAnnulmentRequestDto): string | null {
  if (!a.lastSriCheckAtUtc) return null;
  if (a.lastSriQueryOutcome !== "Success") return SRI_VERIFICATION_FAILED;
  switch (a.lastSriFiscalStatus) {
    case "Authorized":
      return SRI_STILL_AUTHORIZED;
    case "PendingAnnulment":
      return SRI_PENDING_ANNULMENT;
    case "NotAuthorized":
      return SRI_NOT_AUTHORIZED;
    case "Annulled":
      return "ANULADO";
    default:
      return SRI_VERIFICATION_FAILED;
  }
}

/** Mensaje tras presentar/verificar: lo que informó el SRI, o que no pudo verificarse. */
function notifyVerification(updated: RetentionAnnulmentRequestDto, text: (typeof ANNULMENT_ORIGIN_TEXT)["purchase"]) {
  if (updated.status === "Accepted") {
    if (updated.finalizedAtUtc) message.success(text.cancelled);
    else message.warning(`El SRI confirmó ANULADO; la anulación de ${text.the} quedó pendiente y se reintentará.`);
    return;
  }
  if (updated.status !== "PendingSriResolution") return;
  if (!updated.lastSriCheckAtUtc || updated.lastSriQueryOutcome !== "Success") {
    message.warning(SRI_VERIFICATION_FAILED);
    return;
  }
  if (updated.lastSriFiscalStatus === "Authorized") message.info(SRI_STILL_AUTHORIZED);
  else if (updated.lastSriFiscalStatus === "PendingAnnulment") message.info(SRI_PENDING_ANNULMENT);
  else if (updated.lastSriFiscalStatus === "NotAuthorized") message.warning(SRI_NOT_AUTHORIZED);
  else message.warning(SRI_VERIFICATION_FAILED);
}

function CopyValue({ value }: { value: string }) {
  return (
    <>
      <span className="zh-code-value">{value}</span>{" "}
      <ZHIconButton
        icon="content_copy"
        title="Copiar"
        variant="ghost"
        onClick={() => {
          void copyToClipboard(value).then((ok) =>
            ok ? message.success("Copiado.") : message.error("No se pudo copiar."),
          );
        }}
      />
    </>
  );
}

export function RetentionAnnulmentPanel({
  annulment,
  origin,
  canOperate,
  onChanged,
}: RetentionAnnulmentPanelProps) {
  const [modal, setModal] = useState<"submit" | "abandon" | null>(null);
  const [busy, setBusy] = useState<"verify" | "finalize" | null>(null);
  const presentation = statusPresentation(annulment);
  const isOpen = annulment.status === "PendingSubmission" || annulment.status === "PendingSriResolution";
  const text = ANNULMENT_ORIGIN_TEXT[origin];
  const sriCheck = lastSriCheckText(annulment);

  const handleVerified = (updated: RetentionAnnulmentRequestDto) => {
    notifyVerification(updated, text);
    onChanged(updated);
  };

  const verifyWithSri = async () => {
    setBusy("verify");
    try {
      handleVerified(await retentionsService.verifyAnnulmentWithSri(annulment.id));
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: SRI_VERIFICATION_FAILED }));
    }
    setBusy(null);
  };

  const retryFinalization = async () => {
    setBusy("finalize");
    try {
      handleVerified(await retentionsService.retryAnnulmentFinalization(annulment.id));
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: "No se pudo completar la anulación." }));
    }
    setBusy(null);
  };

  return (
    <div className="zh-stack" data-testid="retention-annulment-panel">
      <ZHInfoRow label="Anulación ante el SRI" value={<Badge variant={presentation.variant} label={presentation.label} />} />

      {isOpen ? (
        <ZHPageNotice
          variant="warning"
          message={text.notCancelled}
          detail={
            annulment.status === "PendingSubmission"
              ? "Presente la solicitud en SRI en Línea con los datos de abajo y luego indique \"Ya presenté la solicitud\". Mientras tanto la retención sigue autorizada y la cuenta por pagar no admite pagos ni créditos."
              : "El ERP consulta el estado en el SRI automáticamente; se anulará solo cuando el SRI informe ANULADO. Mientras tanto la retención sigue autorizada y la cuenta por pagar no admite pagos ni créditos."
          }
        />
      ) : null}
      {annulment.status === "Accepted" && annulment.finalizedAtUtc ? (
        <ZHPageNotice variant="success" message={text.cancelled} />
      ) : null}
      {annulment.requiresFinalization ? (
        <ZHPageNotice
          variant="error"
          message={`El SRI confirmó ANULADO, pero ${text.the} todavía no terminó de anularse. Se reintentará automáticamente.`}
          detail={annulment.lastFinalizationError}
        />
      ) : null}
      {annulment.status === "Rejected" || annulment.status === "Expired" ? (
        <ZHPageNotice variant="info" message={text.inForce} />
      ) : null}

      {annulment.status === "PendingSriResolution" && sriCheck ? (
        <ZHPageNotice
          variant={sriCheck === SRI_PENDING_ANNULMENT || sriCheck === SRI_STILL_AUTHORIZED ? "info" : "attention"}
          message={sriCheck}
          detail={`Última verificación: ${formatDateTime(annulment.lastSriCheckAtUtc!)}${annulment.lastSriRawStatus ? ` · SRI: ${annulment.lastSriRawStatus}` : ""}`}
        />
      ) : null}

      {isOpen ? (
        <>
          <ZHInfoRow label="Clave de acceso" value={<CopyValue value={annulment.accessKey} />} wide />
          <ZHInfoRow label="Número de retención" value={<CopyValue value={annulment.retentionNumber} />} />
          <ZHInfoRow label="Fecha de emisión" value={formatDate(annulment.retentionIssueDate)} />
          <ZHInfoRow
            label="Receptor"
            value={<CopyValue value={`${annulment.receptorIdentification} — ${annulment.receptorName}`} />}
            wide
          />
          <ZHInfoRow label="Motivo" value={<CopyValue value={annulment.reason} />} wide />
          <ZHInfoRow label="Plazo ordinario" value={formatDate(annulment.ordinaryDeadline)} />
          <ZHPageNotice
            variant={annulment.isPastOrdinaryDeadline ? "error" : "attention"}
            message={
              annulment.isPastOrdinaryDeadline
                ? "El plazo ordinario (día 7 del mes siguiente a la emisión) ya venció."
                : "Plazo ordinario: hasta el día 7 del mes siguiente a la emisión."
            }
            detail="El ERP no conoce días hábiles ni feriados: valide el plazo efectivo en SRI en Línea. La anulación solo puede solicitarse en línea y puede requerir la aceptación del receptor."
          />
        </>
      ) : null}

      {annulment.submittedOn ? (
        <ZHInfoRow
          label="Presentada al SRI"
          value={`${formatDate(annulment.submittedOn)}${annulment.submissionReference ? ` · ${annulment.submissionReference}` : ""}`}
        />
      ) : null}
      {annulment.resolvedOn ? (
        <ZHInfoRow
          label="ANULADO informado por el SRI"
          value={`${formatDate(annulment.resolvedOn)}${annulment.evidenceReference ? ` · ${annulment.evidenceReference}` : ""}`}
        />
      ) : null}
      {annulment.finalizedAtUtc ? (
        <ZHInfoRow label={text.finalizedLabel} value={formatDateTime(annulment.finalizedAtUtc)} />
      ) : null}

      {canOperate ? (
        <div className="zh-form-actions-row">
          {annulment.status === "PendingSubmission" ? (
            <ZHBtn type="button" variant="primary" size="sm" onClick={() => setModal("submit")}>
              Ya presenté la solicitud
            </ZHBtn>
          ) : null}
          {annulment.status === "PendingSriResolution" ? (
            <ZHBtn type="button" variant="primary" size="sm" disabled={busy !== null} onClick={() => void verifyWithSri()}>
              {busy === "verify" ? "Consultando al SRI..." : "Verificar estado en SRI"}
            </ZHBtn>
          ) : null}
          {annulment.canAbandon ? (
            <ZHBtn type="button" variant="secondary" size="sm" disabled={busy !== null} onClick={() => setModal("abandon")}>
              Desistir
            </ZHBtn>
          ) : null}
          {annulment.requiresFinalization ? (
            <ZHBtn type="button" variant="primary" size="sm" disabled={busy !== null} onClick={() => void retryFinalization()}>
              {busy === "finalize" ? "Reintentando..." : text.complete}
            </ZHBtn>
          ) : null}
        </div>
      ) : null}

      <SubmitAnnulmentModal open={modal === "submit"} annulment={annulment} onClose={() => setModal(null)} onDone={handleVerified} />
      <AbandonAnnulmentModal open={modal === "abandon"} annulment={annulment} onClose={() => setModal(null)} onDone={onChanged} />
    </div>
  );
}
