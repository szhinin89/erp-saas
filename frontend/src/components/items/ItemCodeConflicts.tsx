import { ZHCodeConflicts } from "../zh/ZHCodeConflicts";
import { ZHPageNotice } from "../zh/ZHPageNotice";
import { useI18n } from "../../i18n/i18n";
import type { ItemCodeCandidate } from "../../modules/items/facades/itemLookupFacade";
import { useItemCodeConflicts } from "./useItemCodeConflicts";

/** Official Item contract adapter; diagnostics never replace form or persistence validation. */
export function ItemCodeConflicts({ codes, enabled = true, refreshKey = "" }: {
  codes: ItemCodeCandidate[];
  enabled?: boolean;
  refreshKey?: string;
}) {
  const { t } = useI18n();
  const { conflicts, failed } = useItemCodeConflicts({ codes, enabled, refreshKey });
  return <>
    {failed && <ZHPageNotice variant="warning" message={t("items.codeConflicts.unavailable")} />}
    <ZHCodeConflicts viewLabel={t("items.codeConflicts.view")} conflicts={conflicts.map(c => ({
      code: c.code,
      label: t(`items.codeConflicts.kind.${c.kind}`),
      description: c.itemId ? `${c.itemName} · ${t("items.codeConflicts.rows", { rows: c.rows.join(", ") })}`
        : t("items.codeConflicts.batch", { rows: c.rows.join(", ") }),
      href: c.itemId ? `/products/items?view=${encodeURIComponent(c.itemId)}` : undefined,
    }))} />
  </>;
}
