import { useState, useEffect, useRef, useCallback } from "react";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { itemLookupFacade } from "../../items/facades/itemLookupFacade";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatMoney } from "../../../lib/sanitizers";
import type { ItemDto } from "../../../types/items";
import { useI18n } from "../../../i18n/i18n";
import {
  buildPurchaseItemProfile,
  type ProductProfile as PurchaseProductProfile,
} from "../utils/purchaseItemProfile";

export type ProductProfile = PurchaseProductProfile;

type Props = {
  onSelect: (profile: ProductProfile) => void;
  disabled?: boolean;
  /** Catálogo SRI code→porcentaje (sriLookupService.vatRates()) — única fuente del % mostrado. */
  vatRates?: Record<string, number>;
};

const profileCache = new Map<string, ProductProfile>();

export function ProductPicker({ onSelect, disabled, vatRates }: Props) {
  const { t } = useI18n();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ItemDto[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [focusIdx, setFocusIdx] = useState(-1);
  const wrapRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  const search = useCallback(async (q: string) => {
    if (q.trim().length < 2) {
      setResults([]);
      return;
    }
    setLoading(true);
    try {
      const res = await itemLookupFacade.search({
        search: q.trim(),
        isActive: true,
        pageSize: 12,
      });
      setResults(res.items);
      setFocusIdx(-1);
    } catch {
      setResults([]);
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    if (!open || query.length < 2) return;
    debounceRef.current = setTimeout(() => search(query), 300);
    return () => clearTimeout(debounceRef.current);
  }, [query, open, search]);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node))
        setOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const handleSelect = async (item: ItemDto) => {
    setOpen(false);
    setQuery("");
    setResults([]);

    const cached = profileCache.get(item.id);
    if (cached) {
      onSelect(cached);
      return;
    }

    try {
      const detail = await itemLookupFacade.getById(item.id);
      const profile = buildPurchaseItemProfile(detail, { vatRates });
      profileCache.set(item.id, profile);
      onSelect(profile);
    } catch {
      onSelect(buildPurchaseItemProfile(item));
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!open || results.length === 0) return;
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setFocusIdx((i) => Math.min(i + 1, results.length - 1));
    }
    if (e.key === "ArrowUp") {
      e.preventDefault();
      setFocusIdx((i) => Math.max(i - 1, 0));
    }
    if (e.key === "Enter" && focusIdx >= 0) {
      e.preventDefault();
      void handleSelect(results[focusIdx]);
    }
    if (e.key === "Escape") setOpen(false);
  };

  return (
    <div ref={wrapRef} className="zh-picker">
      <ZhTextInput
        ref={inputRef}
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        onFocus={() => {
          if (query.length >= 2) setOpen(true);
        }}
        onKeyDown={handleKeyDown}
        placeholder={t("purchases.productPicker.placeholder", "Buscar por SKU, nombre...")}
        disabled={disabled}
      />

      {open && query.length >= 2 && (
        <div className="zh-picker__dropdown">
          {loading && (
            <div className="zh-picker__empty">
              {t("common.searching", "Buscando...")}
            </div>
          )}
          {!loading && results.length === 0 && (
            <div className="zh-picker__empty">
              {t("purchases.productPicker.noResults", { query })}
            </div>
          )}
          {results.map((item, i) => {
            const cached = profileCache.get(item.id);
            return (
              <button
                key={item.id}
                type="button"
                className={`zh-picker__result${i === focusIdx ? " zh-picker__result--focused" : ""}`}
                onClick={() => void handleSelect(item)}
                onMouseEnter={() => setFocusIdx(i)}
              >
                <div className="zh-picker__result-main">
                  <div className="zh-picker__result-name">
                    <span className="zh-picker__result-code">{item.sku}</span>
                    {item.shortName}
                  </div>
                  <div className="zh-picker__result-desc">{item.description}</div>
                </div>
                {cached && (
                  <div className="zh-picker__result-extra">
                    <span className="zh-picker__result-extra-label">
                      {t("purchases.productPicker.pvp", "PVP")}:
                    </span>
                    <span className="zh-picker__result-extra-value">
                      $
                      {formatMoney(
                        cached.currentPvp,
                        getDecimalConfig().salesUnitPrice,
                      )}
                    </span>
                    <span className="zh-picker__result-extra-label">
                      {t("purchases.productPicker.vat", "IVA")}:
                    </span>
                    <span className="zh-picker__result-extra-value">
                      {cached.vatRate ?? cached.purchaseVatCode ?? "..."}
                    </span>
                  </div>
                )}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
