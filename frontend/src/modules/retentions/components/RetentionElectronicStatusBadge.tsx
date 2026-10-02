import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { useI18n } from "../../../i18n/i18n";
import type { RetentionElectronicStatus } from "../api/retentionsService";

/**
 * ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — estado electrónico compacto de una retención emitida
 * (Compra y Gasto). El estado lo calcula el backend (`ElectronicDocumentSourceStatusMapper`) desde
 * el ElectronicDocument real; aquí solo se presenta — nunca se reinterpretan estados crudos del SRI.
 * La transmisión es automática al confirmar el documento origen: no hay acción manual asociada.
 */
const STATUS_PRESENTATION: Record<
  RetentionElectronicStatus,
  { variant: BadgeVariant; key: string; fallback: string }
> = {
  Pending: { variant: "neutral", key: "retentions.electronicStatus.pending", fallback: "Pendiente" },
  Processing: { variant: "info", key: "retentions.electronicStatus.processing", fallback: "Procesando" },
  Authorized: { variant: "success", key: "retentions.electronicStatus.authorized", fallback: "Autorizado" },
  Rejected: { variant: "error", key: "retentions.electronicStatus.rejected", fallback: "Rechazado" },
  RequiresReconciliation: {
    variant: "warning",
    key: "retentions.electronicStatus.requiresReconciliation",
    fallback: "Requiere conciliación",
  },
  Discarded: { variant: "neutral", key: "retentions.electronicStatus.discarded", fallback: "Descartado" },
  AnnulmentPending: {
    variant: "warning",
    key: "retentions.electronicStatus.annulmentPending",
    fallback: "Anulación en trámite",
  },
  Annulled: { variant: "neutral", key: "retentions.electronicStatus.annulled", fallback: "Anulado por el SRI" },
};

export function RetentionElectronicStatusBadge({
  status,
}: {
  status: RetentionElectronicStatus | null | undefined;
}) {
  const { t } = useI18n();
  if (!status) return null;
  const presentation = STATUS_PRESENTATION[status];
  if (!presentation) return null;
  return <Badge variant={presentation.variant} label={t(presentation.key, presentation.fallback)} />;
}
