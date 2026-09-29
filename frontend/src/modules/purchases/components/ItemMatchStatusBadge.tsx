import { Badge } from "../../../components/PageShell";
import { useI18n } from "../../../i18n/i18n";
import type { ItemMatchStatus } from "../api/purchaseReceptionService";

const STATUS_VARIANT: Record<
  ItemMatchStatus,
  "green" | "red" | "blue" | "orange"
> = {
  PENDING: "red",
  NEEDS_REVIEW: "orange",
  AUTO_MATCHED: "green",
  MANUALLY_MATCHED: "blue",
};

export function ItemMatchStatusBadge({ status }: { status: ItemMatchStatus }) {
  const { t } = useI18n();
  const label = t(`purchases.itemMatchStatus.${status}`, status);

  return <Badge variant={STATUS_VARIANT[status]} label={label} />;
}
