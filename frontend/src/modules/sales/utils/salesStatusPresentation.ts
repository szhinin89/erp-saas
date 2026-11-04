import type { BadgeVariant } from "../../../components/PageShell";
import type { useI18n } from "../../../i18n/i18n";

type TFunction = ReturnType<typeof useI18n>["t"];

/**
 * SALES-ELECTRONIC-STATUS-CLARITY-01 — presentación de los DOS estados independientes de una
 * venta. Solo visual: no renombra enums ni cambia reglas.
 *
 * - Estado comercial (`SalesInvoice.Status`): `Authorized` se muestra como «Confirmada» — es la
 *   confirmación de la venta en el ERP, NO la autorización del SRI.
 * - Estado electrónico (`ElectronicDocument.CurrentState`, provisto por el backend): se muestra
 *   tal cual llega; nunca se infiere del estado comercial. «Autorizada por SRI» solo para
 *   `Authorized`. Sin dato (`null`/`undefined`) → «No disponible», nunca un estado inventado.
 */

export type SalesStatusPresentation = {
  label: string;
  variant: BadgeVariant;
  /** Explicación breve (tooltip / texto de ayuda); null si el estado no se presta a confusión. */
  hint: string | null;
};

const COMMERCIAL_VARIANT: Record<string, BadgeVariant> = {
  Draft: "warning",
  Authorized: "success",
  Cancelled: "error",
};

export function salesCommercialStatusPresentation(
  status: string,
  t: TFunction,
): SalesStatusPresentation {
  const known = status in COMMERCIAL_VARIANT;
  return {
    label: known ? t(`sales.status.commercial.${status}`) : status,
    variant: COMMERCIAL_VARIANT[status] ?? "neutral",
    hint: status === "Authorized" ? t("sales.status.commercialHint.Authorized") : null,
  };
}

/** Estados reales del motor (`ElectronicDocumentState`) + `None` (sin documento registrado). */
const ELECTRONIC_VARIANT: Record<string, BadgeVariant> = {
  None: "neutral",
  Draft: "neutral",
  XmlGenerated: "neutral",
  Signed: "info",
  Dispatching: "info",
  Sent: "info",
  Received: "info",
  Authorized: "success",
  Rejected: "error",
  Failed: "error",
  DeadLetter: "error",
  AnnulmentPending: "warning",
  Cancelled: "warning",
  Discarded: "neutral",
};

/** Estados cuya etiqueta podría confundirse con «emitida/autorizada» — llevan explicación. */
const ELECTRONIC_WITH_HINT = new Set([
  "None",
  "Draft",
  "XmlGenerated",
  "Signed",
  "Dispatching",
  "Sent",
  "Received",
]);

export function salesElectronicStatusPresentation(
  emissionType: string | null | undefined,
  electronicStatus: string | null | undefined,
  t: TFunction,
): SalesStatusPresentation {
  if (emissionType !== "Electronic") {
    return {
      label: t("sales.status.electronic.NotApplicable"),
      variant: "neutral",
      hint: t("sales.status.electronicHint.NotApplicable"),
    };
  }
  if (!electronicStatus) {
    return {
      label: t("sales.status.electronic.Unavailable"),
      variant: "neutral",
      hint: t("sales.status.electronicHint.Unavailable"),
    };
  }
  if (!(electronicStatus in ELECTRONIC_VARIANT)) {
    // Estado nuevo del motor sin etiqueta propia: se muestra el valor real, sin reinterpretarlo.
    return { label: electronicStatus, variant: "neutral", hint: null };
  }
  return {
    label: t(`sales.status.electronic.${electronicStatus}`),
    variant: ELECTRONIC_VARIANT[electronicStatus],
    hint: ELECTRONIC_WITH_HINT.has(electronicStatus)
      ? t(`sales.status.electronicHint.${electronicStatus}`)
      : null,
  };
}
