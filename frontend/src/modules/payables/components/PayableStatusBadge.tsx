import { Badge } from "../../../components/PageShell";
import type { PayableStatus } from "../api/payablesService";

const STATUS_LABEL: Record<PayableStatus, string> = {
  pending: "Pendiente",
  partiallypaid: "Parcial",
  paid: "Pagada",
  cancelled: "Anulada",
};

const STATUS_VARIANT: Record<PayableStatus, "orange" | "blue" | "green" | "red"> = {
  pending: "orange",
  partiallypaid: "blue",
  paid: "green",
  cancelled: "red",
};

export function PayableStatusBadge({ status }: { status: PayableStatus }) {
  return (
    <Badge
      label={STATUS_LABEL[status] ?? status}
      variant={STATUS_VARIANT[status] ?? "gray"}
      size="md"
    />
  );
}
