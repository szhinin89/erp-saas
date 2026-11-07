import { Badge, type BadgeVariant } from "../../../../components/PageShell";
import type { TFunction } from "../../labels/accountingLabels";
import type { PostingRuleStatus } from "./postingRuleView";

const STATUS: Record<PostingRuleStatus, { key: string; variant: BadgeVariant }> = {
  active: { key: "accounting.postingRules.status.active", variant: "success" },
  inactive: { key: "accounting.postingRules.status.inactive", variant: "neutral" },
  missing: { key: "accounting.postingRules.status.missing", variant: "warning" },
  unsupported: { key: "accounting.postingRules.status.unsupported", variant: "error" },
};

/** Estado de una operación con la variante oficial de `Badge` (lista y detalle). */
export function PostingRuleStatusBadge({ status, t }: { status: PostingRuleStatus; t: TFunction }) {
  const s = STATUS[status];
  return <Badge label={t(s.key)} variant={s.variant} />;
}
