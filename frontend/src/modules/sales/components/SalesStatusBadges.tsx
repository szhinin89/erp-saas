import { Badge } from "../../../components/PageShell";
import { useI18n } from "../../../i18n/i18n";
import {
  salesCommercialStatusPresentation,
  salesElectronicStatusPresentation,
} from "../utils/salesStatusPresentation";

/**
 * SALES-ELECTRONIC-STATUS-CLARITY-01 — badges de estado de una venta (listado, cabecera del
 * detalle y resultado de emisión). Reutilizan `Badge`; la explicación va como `title` (tooltip).
 */
export function SalesCommercialStatusBadge({
  status,
  size,
}: {
  status: string;
  size?: "md";
}) {
  const { t } = useI18n();
  const p = salesCommercialStatusPresentation(status, t);
  return (
    <Badge
      variant={p.variant}
      label={p.label}
      size={size}
      title={p.hint ?? undefined}
      data-testid="sales-commercial-status"
    />
  );
}

export function SalesElectronicStatusBadge({
  emissionType,
  electronicStatus,
  size,
}: {
  emissionType: string | null | undefined;
  electronicStatus: string | null | undefined;
  size?: "md";
}) {
  const { t } = useI18n();
  const p = salesElectronicStatusPresentation(emissionType, electronicStatus, t);
  return (
    <Badge
      variant={p.variant}
      label={p.label}
      size={size}
      title={p.hint ?? undefined}
      data-testid="sales-electronic-status"
    />
  );
}
