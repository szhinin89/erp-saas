import { useEffect, useState, useSyncExternalStore } from "react";
import { usePermissionsUi } from "../../access/usePermissionsUi";
import { itemLookupFacade, codeConflictRevision, subscribeCodeConflicts,
  type ItemCodeCandidate, type ItemCodeConflict } from "../../modules/items/facades/itemLookupFacade";

const isComplete = (c: ItemCodeCandidate) =>
  (c.variant ? c.variant.attributes.length > 0 || !!c.variant.skuOverride?.trim() : !!c.code.trim())
  && (c.kind !== "SupplierCode" || !!c.supplierId);

export function useItemCodeConflicts({ codes, enabled = true, refreshKey = "" }: {
  codes: ItemCodeCandidate[]; enabled?: boolean; refreshKey?: string;
}) {
  // Same permission as the endpoint and the "View item" target; save-time validation still applies.
  const canView = usePermissionsUi().canShow("items.view");
  const revision = useSyncExternalStore(subscribeCodeConflicts, codeConflictRevision);
  const [result, setResult] = useState<{ key: string; conflicts: ItemCodeConflict[]; failed: boolean }>();
  const payload = JSON.stringify(codes.filter(isComplete));
  const key = JSON.stringify([payload, enabled, canView, revision, refreshKey]);
  const needsCheck = enabled && payload !== "[]";
  // A previous response never confirms the current inputs, even before the effect runs.
  const current = result?.key === key ? result : undefined;
  useEffect(() => {
    let cancelled = false;
    setResult(undefined);
    if (!enabled || !canView || payload === "[]") return;
    const timeout = setTimeout(() => {
      void Promise.resolve().then(() => itemLookupFacade.codeConflicts(JSON.parse(payload) as ItemCodeCandidate[])).then(result => {
        if (!cancelled) setResult({ key, conflicts: result, failed: false });
      }).catch(() => { if (!cancelled) setResult({ key, conflicts: [], failed: true }); });
    }, 300);
    return () => { cancelled = true; clearTimeout(timeout); };
  }, [payload, enabled, canView, key]);
  return {
    conflicts: current?.conflicts ?? [],
    failed: current?.failed ?? false,
    pending: needsCheck && canView && !current,
    verified: !needsCheck || (!!current && !current.failed),
  };
}
