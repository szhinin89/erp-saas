import { Badge } from "../../../components/PageShell";
import type { PayableOriginType } from "../api/payablesService";

const ORIGIN_LABEL: Record<PayableOriginType, string> = {
  PurchaseInvoice: "Compra",
  ExpenseDocument: "Gasto",
  Manual: "Manual",
};

const ORIGIN_VARIANT: Record<PayableOriginType, "blue" | "gray"> = {
  PurchaseInvoice: "blue",
  ExpenseDocument: "gray",
  Manual: "gray",
};

export function PayableOriginBadge({ originType }: { originType: PayableOriginType }) {
  return (
    <Badge
      label={ORIGIN_LABEL[originType] ?? originType}
      variant={ORIGIN_VARIANT[originType] ?? "gray"}
      size="md"
    />
  );
}
