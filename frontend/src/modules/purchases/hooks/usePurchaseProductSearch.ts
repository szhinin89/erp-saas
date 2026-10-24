import { useEffect, useState } from "react";
import { itemLookupFacade } from "../../items/facades/itemLookupFacade";
import type { ItemDto } from "../../../types/items";

/** Purchase search: debounce, explicit failure and discard responses for obsolete queries. */
export function usePurchaseProductSearch(query: string, open: boolean, pageSize: number, itemTypeId?: string) {
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState<{ key: string; status: "ready" | "error"; items: ItemDto[] } | null>(null);
  const enabled = open && query.trim().length >= 2;
  const key = JSON.stringify([query.trim(), itemTypeId, attempt]);
  useEffect(() => {
    if (!enabled) return;
    let cancelled = false;
    const timer = setTimeout(async () => {
      try {
        const result = await itemLookupFacade.search({ search: query.trim(), isActive: true, pageSize, itemTypeId });
        if (!cancelled) setState({ key, status: "ready", items: result.items });
      } catch {
        if (!cancelled) setState({ key, status: "error", items: [] });
      }
    }, 300);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [enabled, query, key, pageSize, itemTypeId]);
  const current = state?.key === key ? state : null;
  return {
    status: !enabled ? "idle" : current?.status ?? "loading",
    results: enabled && current?.status === "ready" ? current.items : [],
    retry: () => setAttempt(value => value + 1),
  };
}
