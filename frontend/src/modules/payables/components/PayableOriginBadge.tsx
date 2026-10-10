import { Badge } from "../../../components/PageShell";
import { payableOriginLabel, type PayableOriginType } from "../api/payablesService";

const ORIGIN_VARIANT: Record<PayableOriginType, "blue" | "gray"> = {
  PurchaseInvoice: "blue",
  ExpenseDocument: "gray",
  Manual: "gray",
  InitialBalance: "gray",
};

export function PayableOriginBadge({ originType }: { originType: PayableOriginType }) {
  return (
    <Badge
      label={payableOriginLabel(originType)}
      variant={ORIGIN_VARIANT[originType] ?? "gray"}
      size="md"
    />
  );
}
