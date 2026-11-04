import type { BadgeVariant } from "../../../../components/PageShell";

/** Variante del componente `Badge` (`PageShell.tsx`) por estado — estados reales del motor, no inventados. */
export function electronicDocumentStateBadgeVariant(
  state: string,
): BadgeVariant {
  switch (state) {
    case "Draft":
    case "XmlGenerated":
      return "gray";
    case "Signed":
    case "Dispatching":
    case "Sent":
    case "Received":
      return "blue";
    case "Authorized":
      return "green";
    case "Rejected":
    case "DeadLetter":
    case "Failed":
      return "red";
    case "Cancelled":
    case "AnnulmentPending":
      return "orange";
    case "Discarded":
      return "gray";
    default:
      return "gray";
  }
}

/**
 * SALES-ELECTRONIC-STATUS-CLARITY-01 — estados que pueden confundirse con un comprobante ya
 * autorizado (p. ej. Borrador = sin emitir, Firmado ≠ autorizado). Devuelve la clave i18n de su
 * explicación breve, o null si el estado no la necesita.
 */
const STATES_WITH_HELP = new Set([
  "Draft",
  "XmlGenerated",
  "Signed",
  "Dispatching",
  "Sent",
  "Received",
  "Authorized",
]);

export function electronicDocumentStateHelpKey(state: string): string | null {
  return STATES_WITH_HELP.has(state)
    ? `electronicDocuments.monitor.stateHelp.${state}`
    : null;
}

/** Ícono Material Symbols por estado — acompaña el badge para reconocimiento visual rápido. */
export function electronicDocumentStateIcon(state: string): string {
  switch (state) {
    case "Draft":
      return "schedule";
    case "XmlGenerated":
      return "description";
    case "Signed":
      return "draw";
    case "Dispatching":
      return "outbox";
    case "Sent":
      return "send";
    case "Received":
      return "hourglass_top";
    case "Authorized":
      return "check_circle";
    case "Rejected":
      return "cancel";
    case "Failed":
      return "error";
    case "DeadLetter":
      return "report";
    case "AnnulmentPending":
      return "pending_actions";
    case "Cancelled":
      return "block";
    case "Discarded":
      return "do_not_disturb_on";
    default:
      return "help";
  }
}
