import { useCallback, useEffect, useRef, useState } from "react";
import { ZhTextInput } from "../../../../components/zh/inputs/ZhTextInput";
import { ZHPickerResultItem } from "../../../../components/zh/ZHPickerResultItem";
import { itemLookupFacade } from "../../../items/facades/itemLookupFacade";
import type { ItemDto } from "../../../../types/items";
import { useI18n } from "../../../../i18n/i18n";

export type AdjustmentProductProfile = {
  id: string;
  sku: string;
  name: string;
  baseUomCode: string;
};

type Props = {
  onSelect: (profile: AdjustmentProductProfile) => void;
  disabled?: boolean;
};

/**
 * Picker de producto para líneas de ajuste de inventario — solo ítems que controlan stock
 * (`tracksStock`): un ítem sin control de inventario no tiene saldo que ajustar.
 *
 * Auditoría de reutilización: se revisó `purchases/components/ProductPicker.tsx` (acoplado a
 * `buildPurchaseItemProfile`, dominio de Compras) y `transfers/components/TransferProductPicker.tsx`.
 * No existe un picker de producto genérico en `items/` ni en `components/zh/`. No se reutiliza el
 * de Transferencias a propósito: Ajustes es un tipo documental distinto (así lo declara el propio
 * doc comment de `StockTransferPage`) y además necesita el `baseUomCode` del ítem para resolver la
 * equivalencia de presentación, dato que el perfil de Transferencias no expone ni debe exponer.
 * Sí se reutilizan íntegras las piezas genéricas: `ZhTextInput`, `ZHPickerResultItem`,
 * `itemLookupFacade` (superficie read-only del módulo Items) y las clases `zh-picker*` de zh-ui.css.
 */
export function AdjustmentProductPicker({ onSelect, disabled }: Props) {
  const { t } = useI18n();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ItemDto[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [focusIdx, setFocusIdx] = useState(-1);
  const wrapRef = useRef<HTMLDivElement>(null);
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
      setResults(res.items.filter((i) => i.tracksStock));
      setFocusIdx(-1);
    } catch {
      setResults([]);
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    if (!open || query.length < 2) return;
    debounceRef.current = setTimeout(() => void search(query), 300);
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

  const handleSelect = (item: ItemDto) => {
    setOpen(false);
    setQuery("");
    setResults([]);
    onSelect({
      id: item.id,
      sku: item.sku,
      name: item.shortName,
      baseUomCode: item.defaultUomCode,
    });
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
      handleSelect(results[focusIdx]);
    }
    if (e.key === "Escape") setOpen(false);
  };

  return (
    <div ref={wrapRef} className="zh-picker">
      <ZhTextInput
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        onFocus={() => {
          if (query.length >= 2) setOpen(true);
        }}
        onKeyDown={handleKeyDown}
        placeholder={t(
          "inventory.adjustments.placeholders.searchProduct",
          "Buscar producto por SKU o nombre...",
        )}
        aria-label={t(
          "inventory.adjustments.placeholders.searchProduct",
          "Buscar producto por SKU o nombre...",
        )}
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
              {t(
                "inventory.adjustments.messages.noProductResults",
                "Sin resultados para la búsqueda.",
              )}
            </div>
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
              onClick={() => handleSelect(item)}
              onMouseEnter={() => setFocusIdx(i)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
