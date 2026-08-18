import { useCallback, useEffect, useRef, useState } from "react";
import { ZhTextInput } from "../../../../components/zh/inputs/ZhTextInput";
import { ZHPickerResultItem } from "../../../../components/zh/ZHPickerResultItem";
import { itemLookupFacade } from "../../../items/facades/itemLookupFacade";
import type { ItemDto } from "../../../../types/items";

export type TransferProductProfile = {
  id: string;
  sku: string;
  name: string;
};

type Props = {
  onSelect: (profile: TransferProductProfile) => void;
  disabled?: boolean;
};

/**
 * Picker de producto para líneas de transferencia entre bodegas — solo ítems que controlan
 * stock (`tracksStock`), ya que un ítem sin control de inventario no tiene saldo que mover.
 * Reutiliza `itemLookupFacade` (superficie read-only del módulo Items) y componentes ZH
 * genéricos de picker (`ZhTextInput`, `ZHPickerResultItem`) — mismo patrón que
 * `ProductPicker` de Compras, sin el perfil de costo/PVP que ese módulo no necesita aquí.
 *
 * Auditoría de reutilización: se revisó `frontend/src/modules/purchases/components/ProductPicker.tsx`
 * (mismo patrón de picker con debounce) y `ZhWarehouseSelector` (picker de bodega ya usado en
 * esta pantalla) — no existe un picker de producto genérico en `items/` ni en `inventory/`, y el
 * de Compras está acoplado a `buildPurchaseItemProfile` (dominio de Compras, fuera de alcance
 * aquí) — de ahí este componente local mínimo, reutilizando solo piezas DS/facade genéricas.
 */
export function TransferProductPicker({ onSelect, disabled }: Props) {
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

  const handleSelect = (item: ItemDto) => {
    setOpen(false);
    setQuery("");
    setResults([]);
    onSelect({ id: item.id, sku: item.sku, name: item.shortName });
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
        placeholder="Buscar por SKU o nombre..."
        disabled={disabled}
      />

      {open && query.length >= 2 && (
        <div className="zh-picker__dropdown">
          {loading && <div className="zh-picker__empty">Buscando...</div>}
          {!loading && results.length === 0 && (
            <div className="zh-picker__empty">
              Sin resultados con control de stock para &quot;{query}&quot;.
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
