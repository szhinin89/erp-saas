import { usePurchaseProductSearch } from "../hooks/usePurchaseProductSearch";
import { PurchaseSearchFeedback } from "./PurchaseSearchFeedback";
import { useState, useEffect, useId, useLayoutEffect, useRef } from "react";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZHPickerResultItem } from "../../../components/zh/ZHPickerResultItem";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { itemLookupFacade } from "../../items/facades/itemLookupFacade";
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
  /** Precarga la búsqueda con este texto al montar (p.ej. la descripción XML/TXT
   * de una línea sin producto vinculado) y dispara la búsqueda inicial. Solo se
   * usa como valor inicial de useState — no se vuelve a sincronizar en renders
   * posteriores, así que el usuario puede editar/borrar libremente sin que se
   * pise lo que escribe. No selecciona ni vincula nada automáticamente. */
  initialQuery?: string;
  /** Inside scrollable containers (modals): the results float over the content, anchored to the
   * input width, with their own scroll, instead of being clipped by the container. */
  floating?: boolean;
};

const FLOATING_MAX_HEIGHT = 320;

const profileCache = new Map<string, ProductProfile>();

export function ProductPicker({ onSelect, disabled, vatRates, initialQuery, floating = false }: Props) {
  const { t } = useI18n();
  const [query, setQuery] = useState(initialQuery ?? "");
  const [open, setOpen] = useState(
    () => (initialQuery ?? "").trim().length >= 2,
  );
  const { results, status, retry } = usePurchaseProductSearch(query, open, 12);
  const loading = status === "loading";
  const [focusIdx, setFocusIdx] = useState(-1);
  const wrapRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const listId = useId();
  const showDropdown = open && query.length >= 2;
  useEffect(() => setFocusIdx(-1), [query, status]);

  // Floating mode: fixed panel measured against its own containing block (the transformed modal
  // or the viewport), limited to the space left inside that container; opens upward if needed.
  useLayoutEffect(() => {
    if (!floating || !showDropdown) return;
    const panel = dropdownRef.current;
    const anchor = wrapRef.current;
    if (!panel || !anchor) return;
    const position = () => {
      const gap = 4;
      const margin = 8;
      const input = anchor.getBoundingClientRect();
      panel.style.left = "0px";
      panel.style.top = "0px";
      const origin = panel.getBoundingClientRect();
      const container = anchor.closest(".zh-modal")?.getBoundingClientRect()
        ?? { top: 0, bottom: document.documentElement.clientHeight };
      const desired = Math.min(panel.scrollHeight, FLOATING_MAX_HEIGHT);
      const below = Math.max(0, container.bottom - input.bottom - gap - margin);
      const above = Math.max(0, input.top - container.top - gap - margin);
      const openAbove = desired > below && above > below;
      const height = Math.min(desired, openAbove ? above : below);
      panel.style.width = `${input.width}px`;
      panel.style.maxHeight = `${height}px`;
      panel.style.left = `${input.left - origin.left}px`;
      panel.style.top = `${(openAbove ? input.top - gap - height : input.bottom + gap) - origin.top}px`;
    };
    position();
    const observer = typeof ResizeObserver !== "undefined" ? new ResizeObserver(position) : null;
    observer?.observe(anchor);
    window.addEventListener("resize", position);
    window.addEventListener("scroll", position, true);
    return () => {
      observer?.disconnect();
      window.removeEventListener("resize", position);
      window.removeEventListener("scroll", position, true);
    };
  }, [floating, showDropdown, results, loading, status]);

  // Keyboard navigation keeps the active option visible inside the list's own scroll.
  useEffect(() => {
    if (focusIdx < 0) return;
    document.getElementById(`${listId}-${focusIdx}`)?.scrollIntoView?.({ block: "nearest" });
  }, [focusIdx, listId]);

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
    if (e.key === "Escape") { setOpen(false); return; }
    if (!open || results.length === 0) return;
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setFocusIdx((i) => Math.min(i + 1, results.length - 1));
    }
    if (e.key === "ArrowUp") {
      e.preventDefault();
      setFocusIdx((i) => Math.max(i - 1, 0));
    }
    if (e.key === "Enter" && focusIdx >= 0 && results[focusIdx]) {
      e.preventDefault();
      void handleSelect(results[focusIdx]);
    }
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
        role="combobox"
        aria-autocomplete="list"
        aria-expanded={showDropdown}
        aria-controls={listId}
        aria-activedescendant={showDropdown && focusIdx >= 0 ? `${listId}-${focusIdx}` : undefined}
        aria-label={t("purchases.productPicker.placeholder", "Buscar por SKU, nombre...")}
        placeholder={t("purchases.productPicker.placeholder", "Buscar por SKU, nombre...")}
        disabled={disabled}
      />

      {showDropdown && (
        <div ref={dropdownRef} id={listId} role="listbox"
          className={`zh-picker__dropdown${floating ? " zh-picker__dropdown--floating" : ""}`}>
          <PurchaseSearchFeedback status={status} empty={results.length === 0} query={query} onRetry={retry} />
          {results.map((item, i) => {
            const cached = profileCache.get(item.id);
            return (
              <ZHPickerResultItem
                key={item.id}
                id={`${listId}-${i}`}
                role="option"
                tabIndex={-1}
                selected={i === focusIdx}
                title={
                  <>
                    <span className="zh-picker__result-code">{item.sku}</span>
                    {item.shortName}
                  </>
                }
                subtitle={
                  <>
                    {item.description}
                    {item.defaultUomAbbrev && (
                      <span className="zh-picker__result-presentation">
                        {t("purchases.productPicker.presentation", "Presentación")}: {item.defaultUomAbbrev}
                      </span>
                    )}
                  </>
                }
                meta={
                  cached && (
                    <div className="zh-picker__result-extra">
                      <span className="zh-picker__result-extra-label">
                        {t("purchases.productPicker.pvp", "PVP")}:
                      </span>
                      <span className="zh-picker__result-extra-value">
                        <ZHMoneyValue
                          value={cached.currentPvp}
                          precision="salesUnitPrice"
                        />
                      </span>
                      <span className="zh-picker__result-extra-label">
                        {t("purchases.productPicker.vat", "IVA")}:
                      </span>
                      <span className="zh-picker__result-extra-value">
                        {cached.vatRate ?? cached.purchaseVatCode ?? "..."}
                      </span>
                    </div>
                  )
                }
                onClick={() => void handleSelect(item)}
                onMouseEnter={() => setFocusIdx(i)}
              />
            );
          })}
        </div>
      )}
    </div>
  );
}
