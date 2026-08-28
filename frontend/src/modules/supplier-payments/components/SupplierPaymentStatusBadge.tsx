import { Badge } from "../../../components/PageShell";
import type { SupplierPaymentStatus } from "../api/supplierPaymentService";

const STATUS_LABEL: Record<SupplierPaymentStatus, string> = {
  Confirmed: "Confirmado",
  Reversed: "Reversado",
};

const STATUS_VARIANT: Record<SupplierPaymentStatus, "green" | "red"> = {
  Confirmed: "green",
  Reversed: "red",
};

export function SupplierPaymentStatusBadge({ status }: { status: SupplierPaymentStatus }) {
  return (
    <Badge label={STATUS_LABEL[status] ?? status} variant={STATUS_VARIANT[status] ?? "gray"} size="md" />
  );
}
