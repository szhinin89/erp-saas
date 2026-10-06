import { useEffect, useRef, useState } from "react";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZHPickerResultItem } from "../../../components/zh/ZHPickerResultItem";
import { useI18n } from "../../../i18n/i18n";
import type { ItemDto } from "../../../types/items";
import { useItemLookupSearch } from "../hooks/useItemLookupSearch";

export interface ItemLookupPickerProps {
  /** Ítem elegido; el picker se limpia para la siguiente búsqueda. */
  onSelect: (item: ItemDto) => void;
  placeholder: string;
  /** Texto cuando la búsqueda no devuelve ítems elegibles. */
  emptyText: (query: string) => string;
  disabled?: boolean;
  pageSize?: number;
  /** Solo ítems con control de stock, filtrado en el backend (ver useItemLookupSearch). */
  participatesInInventory?: boolean;
}

/**
 * ZH-PRODUCT-SELECTOR-SSOT-01 — picker manual de Item del owner `items` (Design System `zh-picker`
 * + `ZHPickerResultItem`) sobre `useItemLookupSearch`. Contrato mínimo: busca por SKU/nombre y
 * entrega el `ItemDto` elegido; cada consumidor arma su propio perfil de línea. Teclado: ↑/↓
 * recorren, Enter elige, Escape cierra. Subscribers externos lo consumen vía `itemLookupFacade`.
 */
export function ItemLookupPicker({
  onSelect,
  placeholder,
  emptyText,
  disabled,
  pageSize,
  participatesInInventory,
}: ItemLookupPickerProps) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [focusIdx, setFocusIdx] = useState(-1);
  const wrapRef = useRef<HTMLDivElement>(null);
  const lookup = useItemLookupSearch({ pageSize, enabled: open, participatesInInventory });
  const { results } = lookup;

  useEffect(() => {
    setFocusIdx(-1);
  }, [results]);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const select = (item: ItemDto) => {
    setOpen(false);
    lookup.reset();
    onSelect(item);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Escape") {
      setOpen(false);
      return;
    }
    if (!open || results.length === 0) return;
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setFocusIdx((i) => Math.min(i + 1, results.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setFocusIdx((i) => Math.max(i - 1, 0));
    } else if (e.key === "Enter" && focusIdx >= 0) {
      e.preventDefault();
      select(results[focusIdx]);
    }
  };

  return (
    <div ref={wrapRef} className="zh-picker">
      <ZhTextInput
        value={lookup.query}
        onChange={(e) => {
          lookup.setQuery(e.target.value);
          setOpen(true);
        }}
        onFocus={() => {
          if (lookup.active) setOpen(true);
        }}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
        aria-label={placeholder}
        disabled={disabled}
      />

      {open && lookup.active && (
        <div className="zh-picker__dropdown">
          {lookup.loading && (
            <div className="zh-picker__empty">{t("common.searching", "Buscando...")}</div>
          )}
          {!lookup.loading && lookup.error && (
            <div className="zh-picker__empty" role="alert">
              {lookup.error}
            </div>
          )}
          {!lookup.loading && !lookup.error && results.length === 0 && (
            <div className="zh-picker__empty">{emptyText(lookup.query.trim())}</div>
          )}
          {results.map((item, i) => (
            <ZHPickerResultItem
              key={item.id}
              selected={i === focusIdx}
              title={
                <>
                  <span className="zh-picker__result-code">{item.sku}</span>
                  {item.shortName}
                </>
              }
              subtitle={item.description}
              onClick={() => select(item)}
              onMouseEnter={() => setFocusIdx(i)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
