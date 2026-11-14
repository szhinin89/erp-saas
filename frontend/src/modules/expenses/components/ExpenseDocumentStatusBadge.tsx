import { Badge } from "../../../components/PageShell";
import { useI18n } from "../../../i18n/i18n";
import type { ExpenseStatus } from "../api/expenseDocumentService";

const STATUS_LABEL_KEY: Record<ExpenseStatus, string> = {
  Draft: "expenses.documents.status.draft",
  Confirmed: "expenses.documents.status.confirmed",
  Cancelled: "expenses.documents.status.cancelled",
};

const STATUS_VARIANT: Record<ExpenseStatus, "gray" | "green" | "red"> = {
  Draft: "gray",
  Confirmed: "green",
  Cancelled: "red",
};

export function ExpenseDocumentStatusBadge({ status }: { status: ExpenseStatus }) {
  const { t } = useI18n();
  const labelKey = STATUS_LABEL_KEY[status];
  return (
    <Badge
      label={labelKey ? t(labelKey) : status}
      variant={STATUS_VARIANT[status] ?? "gray"}
      size="md"
    />
  );
}
