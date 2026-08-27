import { Badge } from "../../../components/PageShell";
import type { ExpenseStatus } from "../api/expenseDocumentService";

const STATUS_LABEL: Record<ExpenseStatus, string> = {
  Draft: "Borrador",
  Confirmed: "Confirmado",
  Cancelled: "Anulado",
};

const STATUS_VARIANT: Record<ExpenseStatus, "gray" | "green" | "red"> = {
  Draft: "gray",
  Confirmed: "green",
  Cancelled: "red",
};

export function ExpenseDocumentStatusBadge({ status }: { status: ExpenseStatus }) {
  return (
    <Badge
      label={STATUS_LABEL[status] ?? status}
      variant={STATUS_VARIANT[status] ?? "gray"}
      size="md"
    />
  );
}
