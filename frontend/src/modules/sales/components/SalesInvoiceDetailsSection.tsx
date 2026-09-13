import { useCallback, useEffect, useRef, useState } from "react";
import type { SalesInvoiceDetailDto } from "../api/salesService";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";
import { invoiceItemSearchService } from "../api/invoiceItemSearchService";
import type { InvoiceItemSearchResultDto } from "../api/invoiceItemSearchService";
import type { WarehouseDto, ItemWarehouseAvailabilityDto } from "../../inventory/types";
import { ZhWarehouseSelector } from "../../../components/zh/inputs/ZhWarehouseSelector";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { SalesItemSearchResultsGrid } from "./SalesItemSearchResultsGrid";
import { SalesInvoiceLinesGrid } from "./SalesInvoiceLinesGrid";
import { formatVatLabel } from "../utils/salesCalc";
import "../styles/sales-product-card.css";

export type LineWithKey = SalesLineFormValues;

interface SalesInvoiceDetailsSectionProps {
  lines: LineWithKey[];
  backendLines?: SalesInvoiceDetailDto[];
  readOnly: boolean;
  disabled: boolean;
  onRemoveLine: (key: number) => void;
  onUpdateLine: (key: number, field: string, value: unknown) => void;
  onAddItemLine: (item: InvoiceItemSearchResultDto) => Promise<void>;
  onUpdateLineWarehouse: (
    key: number,
    warehouseId: string,
    option?: ItemWarehouseAvailabilityDto,
  ) => void;
  /** SALES-PRESENTATIONS-03: cambio de presentación (unidad/caja/pack) de una línea. Opcional
   * para no forzar a todo consumidor existente a pasarlo — sin este callback, el selector de
   * presentación simplemente no se renderiza (ver SalesInvoiceLineGridRow). */
  onUpdateLinePresentation?: (key: number, packagingLevelId: string) => void;
  warehouses: WarehouseDto[];
  selectedWarehouseId: string;
  onWarehouseChange: (id: string) => void;
  vatRates?: Record<string, number>;
  /** Cambia (se incrementa) cada vez que el buscador de productos debe recibir foco — UX retail. */
  focusSignal?: number;
}

export function SalesInvoiceDetailsSection({
  lines,
  backendLines,
  readOnly,
  disabled,
  onRemoveLine,
  onUpdateLine,
  onAddItemLine,
  onUpdateLineWarehouse,
  onUpdateLinePresentation,
  warehouses,
  selectedWarehouseId,
  onWarehouseChange,
  vatRates,
  focusSignal,
}: SalesInvoiceDetailsSectionProps) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<InvoiceItemSearchResultDto[]>([]);
  const [open, setOpen] = useState(false);
  const [focusIdx, setFocusIdx] = useState(-1);
  const [loading, setLoading] = useState(false);
  const [selecting, setSelecting] = useState(false);
  const searchRef = useRef<HTMLDivElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const resultRefs = useRef<Array<HTMLDivElement | null>>([]);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);
  const searchVersionRef = useRef(0);

  // UX retail: foco automático al buscador de productos al entrar a "Nueva
  // Venta" y después de cada "Nueva venta" tras emitir (ver productSearchFocusKey en useSalesPage.ts).
  useEffect(() => {
    if (!disabled) searchInputRef.current?.focus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focusSignal]);

  // Atajo global F2: reenfoca el buscador de productos desde cualquier parte de la página.
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === "F2" && !disabled) {
        e.preventDefault();
        searchInputRef.current?.focus();
      }
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [disabled]);

  // Mantiene visible el resultado resaltado al navegar la lista con teclado.
  useEffect(() => {
    if (focusIdx >= 0)
      resultRefs.current[focusIdx]?.scrollIntoView({ block: "nearest" });
  }, [focusIdx]);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    if (!open || query.length < 2) {
      setResults([]);
      setLoading(false);
      return;
    }
    setLoading(true);
    debounceRef.current = setTimeout(async () => {
      const version = ++searchVersionRef.current;
      try {
        const res = await invoiceItemSearchService.search({
          q: query.trim(),
          warehouseId: selectedWarehouseId || undefined,
          pageSize: 10,
        });
        if (version === searchVersionRef.current) {
          setResults(res);
          setFocusIdx(-1);
        }
      } catch {
        if (version === searchVersionRef.current) setResults([]);
      } finally {
        if (version === searchVersionRef.current) setLoading(false);
      }
    }, 300);
    return () => clearTimeout(debounceRef.current);
  }, [query, open, selectedWarehouseId]);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (searchRef.current && !searchRef.current.contains(e.target as Node))
        setOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const selectItem = useCallback(
    async (item: InvoiceItemSearchResultDto) => {
      setOpen(false);
      setQuery("");
      setResults([]);
      setSelecting(true);
      try {
        await onAddItemLine(item);
      } finally {
        setSelecting(false);
      }
    },
    [onAddItemLine],
  );

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      if (!open || results.length === 0) return;
      e.preventDefault();
      setFocusIdx((i) => Math.min(i + 1, results.length - 1));
      return;
    }
    if (e.key === "ArrowUp") {
      if (!open || results.length === 0) return;
      e.preventDefault();
      setFocusIdx((i) => Math.max(i - 1, 0));
      return;
    }
    if (e.key === "Escape") {
      setOpen(false);
      return;
    }
    if (e.key !== "Enter") return;
    e.preventDefault();
    if (query.trim().length < 2) return;

    // Navegación por teclado (usuario ya resaltó un resultado): respeta esa elección.
    if (open && focusIdx >= 0 && results[focusIdx]) {
      void selectItem(results[focusIdx]);
      return;
    }
    // Resultados ya cargados (búsqueda con debounce ya resuelta): agrega el primero —
    // caso típico de lector de código de barras USB (código + Enter).
    if (open && !loading && results.length > 0) {
      void selectItem(results[0]);
      return;
    }
    // El debounce de 300ms aún no resolvió (Enter llegó antes) — se fuerza una
    // búsqueda inmediata para no perder el escaneo.
    void (async () => {
      clearTimeout(debounceRef.current);
      setLoading(true);
      const version = ++searchVersionRef.current;
      try {
        const res = await invoiceItemSearchService.search({
          q: query.trim(),
          warehouseId: selectedWarehouseId || undefined,
          pageSize: 10,
        });
        if (version !== searchVersionRef.current) return;
        setResults(res);
        if (res.length > 0) void selectItem(res[0]);
      } catch {
        if (version === searchVersionRef.current) setResults([]);
      } finally {
        if (version === searchVersionRef.current) setLoading(false);
      }
    })();
  };

  const vatLabel = (code: string) => {
    if (!code) return "Sin IVA configurado";
    const rate = vatRates?.[code];
    if (rate !== undefined) return formatVatLabel(rate);
    // Catálogo aún no cargado — nunca se infiere el porcentaje desde el código.
    return "Cargando...";
  };

  return (
    <>
      {/* Search bar */}
      <div className="sf-searchbar">
        <div className="sf-searchbar__input-wrap" ref={searchRef}>
          <span className="material-symbols-outlined sf-searchbar__icon">
            barcode_scanner
          </span>
          <input
            ref={searchInputRef}
            className={`sf-searchbar__input${selecting ? " sf-searchbar__input--selecting" : ""}`}
            type="text"
            placeholder={
              selecting
                ? "Cargando producto…"
                : "Escribe el nombre del producto o escanea el código... (F2)"
            }
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setOpen(true);
            }}
            onFocus={() => {
              if (query.length >= 2) setOpen(true);
            }}
            onKeyDown={handleKeyDown}
            disabled={disabled || selecting}
          />
          {open && query.length >= 2 && (
            <div className="sf-search-dropdown">
              {loading ? (
                <div className="sf-search-loading">
                  <span className="sf-search-spinner" />
                  <span>Buscando…</span>
                </div>
              ) : results.length === 0 ? (
                <div className="pdl-search__empty">
                  Sin resultados para &ldquo;{query}&rdquo;
                </div>
              ) : (
                <SalesItemSearchResultsGrid
                  results={results}
                  searchTerm={query}
                  focusIndex={focusIdx}
                  onAdd={(item) => void selectItem(item)}
                  onHoverIndex={setFocusIdx}
                  registerResultRef={(i, el) => {
                    resultRefs.current[i] = el;
                  }}
                />
              )}
            </div>
          )}
        </div>
        <ZHFieldHelp helpKey={HELP_KEYS.SALES_PRODUCT_SEARCH} />
        <ZhWarehouseSelector
          value={selectedWarehouseId || null}
          onChange={(id) => onWarehouseChange(id)}
          fallbackWarehouses={warehouses}
          disabled={disabled || selecting}
          placeholder="Seleccione bodega"
        />
      </div>

      {/* Product lines — grilla horizontal con cabecera de columnas visible
          (SALES-INVOICE-LINES-GRID-UX-01B), mismo patrón que el buscador de arriba. */}
      <div className="sf-products">
        <SalesInvoiceLinesGrid
          lines={lines}
          backendLines={backendLines}
          disabled={disabled}
          readOnly={readOnly}
          vatLabel={vatLabel}
          vatRates={vatRates}
          warehouses={warehouses}
          selectedWarehouseId={selectedWarehouseId}
          onUpdate={onUpdateLine}
          onUpdateWarehouse={onUpdateLineWarehouse}
          onUpdatePresentation={onUpdateLinePresentation}
          onRemove={onRemoveLine}
        />
      </div>
    </>
  );
}
