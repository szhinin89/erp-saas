import { useCallback, useEffect, useRef, useState } from "react";
import { formatApiRequestError } from "../../lib/apiError";
import { itemLookupFacade } from "../facades/itemLookupFacade";
import type { ItemDto } from "../../../types/items";

/** Longitud mínima del texto antes de consultar al backend. */
export const ITEM_LOOKUP_MIN_LENGTH = 2;
/** Espera tras la última tecla antes de consultar. */
export const ITEM_LOOKUP_DEBOUNCE_MS = 300;

export interface ItemLookupSearchOptions {
  /** Tamaño de página pedido al backend (GET /items). */
  pageSize?: number;
  /** false = no consulta (p. ej. dropdown cerrado); limpia resultados. */
  enabled?: boolean;
  /**
   * Solo ítems con control de stock (Inventario). Lo filtra el backend antes de ordenar y paginar
   * (ZH-INVENTORY-STOCK-ITEM-LOOKUP-01): nunca se filtra una página parcial en React.
   */
  tracksStock?: boolean;
}

export interface ItemLookupSearchState {
  query: string;
  setQuery: (query: string) => void;
  results: ItemDto[];
  loading: boolean;
  error: string;
  /** true cuando el texto alcanza la longitud mínima (hay búsqueda en curso o resultados). */
  active: boolean;
  reset: () => void;
}

/**
 * ZH-PRODUCT-SELECTOR-SSOT-01 — única implementación de la búsqueda manual de Items (selección
 * administrativa/transaccional por SKU, nombre o descripción). Delega búsqueda y orden en el
 * backend (GET /api/v1/items: contiene sobre SKU/nombre/descripción, solo activos, orden por SKU)
 * — no re-rankea en React. Aporta lo común a todos los pickers: longitud mínima, debounce,
 * descarte de respuestas obsoletas (una respuesta lenta nunca pisa a una búsqueda más nueva) y
 * estado de error. No sirve a Ventas/POS (ranking por barcode/SKU exacto + stock + precio en
 * GET /sales/item-search) ni al matching de recepción de Compras.
 */
export function useItemLookupSearch({
  pageSize = 12,
  enabled = true,
  tracksStock,
}: ItemLookupSearchOptions = {}): ItemLookupSearchState {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ItemDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const requestRef = useRef(0);

  const term = query.trim();
  const active = term.length >= ITEM_LOOKUP_MIN_LENGTH;

  useEffect(() => {
    const request = ++requestRef.current;
    if (!enabled || !active) {
      setResults([]);
      setLoading(false);
      setError("");
      return;
    }
    // Durante el debounce ya se muestra "buscando" (nunca un "sin resultados" prematuro).
    setLoading(true);
    setError("");
    const timer = setTimeout(async () => {
      try {
        const page = await itemLookupFacade.search({
          search: term,
          isActive: true,
          pageSize,
          ...(tracksStock === undefined ? {} : { tracksStock }),
        });
        if (request !== requestRef.current) return;
        setResults(page.items);
      } catch (err) {
        if (request !== requestRef.current) return;
        setResults([]);
        setError(formatApiRequestError(err, { generic: "No se pudo buscar productos." }));
      }
      if (request === requestRef.current) setLoading(false);
    }, ITEM_LOOKUP_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [term, active, enabled, pageSize, tracksStock]);

  const reset = useCallback(() => {
    requestRef.current++;
    setQuery("");
    setResults([]);
    setLoading(false);
    setError("");
  }, []);

  return { query, setQuery, results, loading, error, active, reset };
}
