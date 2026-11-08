import type {
  ElectronicDocumentDiagnosticDto,
  ElectronicDocumentMessageDto,
  ElectronicDocumentTimelineEventDto,
} from "../../../../components/zh/electronicDocuments/electronicDocumentDiagnosticTypes";

/**
 * SRI-MONITOR-SHEET-01 — lectura de la ficha operativa del Monitor. Solo deriva de campos reales del
 * diagnóstico (estado, número/fecha de autorización, etapa del rechazo calculada por el backend,
 * historial auditado); nunca infiere un resultado SRI que el backend no informó. Separa tres planos:
 * estado documental (`currentState`), recepción SRI y autorización SRI.
 */

/** Estados en los que el comprobante todavía no se transmitió al SRI (pipeline previo al envío o descartado). */
const NOT_SENT_STATES = new Set(["Draft", "XmlGenerated", "Failed", "Discarded"]);

export type SriReceptionStatus = "received" | "returned" | "unconfirmed" | "notSent";
export type SriAuthorizationStatus = "authorized" | "notAuthorized" | "pending" | "none";

/** Evento más reciente de una acción dentro del historial devuelto (el backend envía los más recientes). */
export function latestEvent(
  timeline: ElectronicDocumentTimelineEventDto[],
  action: string,
): ElectronicDocumentTimelineEventDto | null {
  let found: ElectronicDocumentTimelineEventDto | null = null;
  for (const ev of timeline)
    if (ev.action === action && (!found || ev.occurredAtUtc > found.occurredAtUtc)) found = ev;
  return found;
}

/** Respuesta SRI más reciente (los mensajes llegan en orden cronológico, pero no se asume). */
export function latestMessage(
  messages: ElectronicDocumentMessageDto[],
): ElectronicDocumentMessageDto | null {
  let found: ElectronicDocumentMessageDto | null = null;
  for (const m of messages) if (!found || m.occurredAtUtc >= found.occurredAtUtc) found = m;
  return found;
}

const rejectionStage = (d: ElectronicDocumentDiagnosticDto) =>
  d.currentState === "Rejected" ? (d.retransmission?.rejectionStage ?? null) : null;

export function sriReceptionStatus(d: ElectronicDocumentDiagnosticDto): SriReceptionStatus {
  if (d.technicalInfo.authorizationNumber || d.currentState === "Authorized") return "received";
  const stage = rejectionStage(d);
  if (stage === "Reception") return "returned";
  if (stage === "Authorization") return "received";
  if (d.currentState === "Received" || latestEvent(d.timeline, "Received")) return "received";
  if (NOT_SENT_STATES.has(d.currentState)) return "notSent";
  return "unconfirmed";
}

export function sriAuthorizationStatus(d: ElectronicDocumentDiagnosticDto): SriAuthorizationStatus {
  if (d.technicalInfo.authorizationNumber || d.currentState === "Authorized") return "authorized";
  if (d.currentState === "Rejected") return "notAuthorized";
  if (NOT_SENT_STATES.has(d.currentState)) return "none";
  return "pending";
}

/** Clave i18n del significado del estado documental (Rechazado distingue la etapa informada por el backend). */
export function stateMeaningKey(d: ElectronicDocumentDiagnosticDto): string {
  const base = "electronicDocuments.monitor.sheet.meaning";
  if (d.currentState === "Rejected") {
    const stage = rejectionStage(d);
    return stage === "Reception"
      ? `${base}.RejectedReception`
      : stage === "Authorization"
        ? `${base}.RejectedAuthorization`
        : `${base}.Rejected`;
  }
  return `${base}.${d.currentState}`;
}

/** Estados que se presentan como incidencia principal (aviso), con su severidad. */
export function incidenceVariant(state: string): "error" | "warning" | null {
  switch (state) {
    case "Rejected":
    case "DeadLetter":
    case "Failed":
      return "error";
    case "Dispatching":
      return "warning";
    default:
      return null;
  }
}

/** Una respuesta se pliega cuando su detalle supera este largo: el mensaje principal sigue visible. */
export const LONG_RESPONSE_CHARS = 240;

export function isLongResponse(m: ElectronicDocumentMessageDto): boolean {
  return (m.additionalInfo?.length ?? 0) + m.message.length > LONG_RESPONSE_CHARS;
}
