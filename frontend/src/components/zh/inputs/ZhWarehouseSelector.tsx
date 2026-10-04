import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Badge } from "../../PageShell";
import { stockService } from "../../../modules/inventory/stock/api/stockService";
import type { ItemWarehouseAvailabilityDto } from "../../../modules/inventory/stock/api/stockService";
import type { WarehouseDto } from "../../../modules/inventory/warehouses/api/warehouseService";
import "./ZhWarehouseSelector.css";

export type { ItemWarehouseAvailabilityDto };

interface ZhWarehouseSelectorProps {
  value: string | null;
  onChange: (
    warehouseId: string,
    option?: ItemWarehouseAvailabilityDto,
  ) => void;
  /** Si se provee, consulta disponibilidad real por bodega para este item. */
  itemId?: string | null;
  /** Nombres a mostrar cuando no hay itemId, o mientras carga/si falla la consulta. */
  fallbackWarehouses: WarehouseDto[];
  /** Bodega del encabezado — se marca con ⭐ "Bodega por defecto". */
  defaultWarehouseId?: string | null;
  disabled?: boolean;
  placeholder?: string;
}

function availabilityVariant(available: number): "green" | "orange" | "red" {
  if (available <= 0) return "red";
  if (available <= 5) return "orange";
  return "green";
}

export function ZhWarehouseSelector({
  value,
  onChange,
  itemId,
  fallbackWarehouses,
  defaultWarehouseId,
  disabled,
  placeholder,
}: ZhWarehouseSelectorProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [focusIdx, setFocusIdx] = useState(-1);
  const [loading, setLoading] = useState(false);
  const [options, setOptions] = useState<ItemWarehouseAvailabilityDto[] | null>(
    null,
  );
  const wrapRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);


  const rows: ItemWarehouseAvailabilityDto[] = useMemo(() => {
    const base: ItemWarehouseAvailabilityDto[] =
      options ??
      fallbackWarehouses.map((w) => ({
        warehouseId: w.id,
        warehouseName: w.name,
        available: 0,
        reserved: 0,
        canSell: true,
      }));
    return base;
  }, [options, fallbackWarehouses]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    const list = q
      ? rows.filter((r) => r.warehouseName.toLowerCase().includes(q))
      : rows;
    if (!options) return list; // sin datos de disponibilidad: mantener orden original
    return [...list].sort(
      (a, b) =>
        b.available - a.available ||
        a.warehouseName.localeCompare(b.warehouseName),
    );
  }, [rows, query, options]);

  const selectedName =
    options?.find((o) => o.warehouseId === value)?.warehouseName ??
    fallbackWarehouses.find((w) => w.id === value)?.name ??
    "";

  useLayoutEffect(() => {
    if (!open) return;
    const panel = panelRef.current;
    const anchor = wrapRef.current;
    if (!panel || !anchor) return;
    const position = () => {
      const bounds = anchor.getBoundingClientRect();
      const margin = 8;
      const gap = 4;
      const viewportWidth = document.documentElement.clientWidth;
      const viewportHeight = document.documentElement.clientHeight;
      const width = Math.min(Math.max(bounds.width, 260), 340, Math.max(0, viewportWidth - margin * 2));
      panel.style.width = `${width}px`;
      const list = panel.querySelector<HTMLElement>(".zh-wh-selector__list");
      const desiredHeight = (inputRef.current?.offsetHeight ?? 0) + Math.min(list?.scrollHeight ?? 0, 280) + 2;
      const below = Math.max(0, viewportHeight - bounds.bottom - gap - margin);
      const above = Math.max(0, bounds.top - gap - margin);
      const openAbove = desiredHeight > below && above > below;
      const height = Math.min(desiredHeight, openAbove ? above : below);
      panel.style.left = `${Math.max(margin, Math.min(bounds.left, viewportWidth - width - margin))}px`;
      panel.style.top = `${openAbove ? bounds.top - gap - height : bounds.bottom + gap}px`;
      panel.style.maxHeight = `${height}px`;
      panel.style.visibility = "visible";
    };
    position();
    const observer = typeof ResizeObserver !== "undefined" ? new ResizeObserver(position) : null;
    observer?.observe(anchor);
    if (inputRef.current) observer?.observe(inputRef.current);
    const list = panel.querySelector(".zh-wh-selector__list");
    if (list) observer?.observe(list);
    window.addEventListener("resize", position);
    window.addEventListener("scroll", position, true);
    return () => {
      observer?.disconnect();
      window.removeEventListener("resize", position);
      window.removeEventListener("scroll", position, true);
    };
  }, [open, loading, filtered]);

  useLayoutEffect(() => {
    if (open) inputRef.current?.focus({ preventScroll: true });
  }, [open]);

  useEffect(() => {
    if (!open) return;
    setQuery("");
    setFocusIdx(-1);
    if (!itemId) {
      setOptions(null);
      return;
    }
    let cancelled = false;
    setLoading(true);
    stockService
      .getWarehouseAvailability(itemId)
      .then((data) => {
        if (!cancelled) setOptions(data);
      })
      .catch(() => {
        if (!cancelled) setOptions(null);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, itemId]);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node) &&
          !panelRef.current?.contains(e.target as Node))
        setOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const select = useCallback(
    (row: ItemWarehouseAvailabilityDto) => {
      onChange(row.warehouseId, options ? row : undefined);
      setOpen(false);
    },
    [onChange, options],
  );

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Escape") {
      setOpen(false);
      wrapRef.current?.querySelector("button")?.focus({ preventScroll: true });
      return;
    }
    if (!open || filtered.length === 0) return;
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setFocusIdx((i) => Math.min(i + 1, filtered.length - 1));
    }
    if (e.key === "ArrowUp") {
      e.preventDefault();
      setFocusIdx((i) => Math.max(i - 1, 0));
    }
    if (e.key === "Enter" && focusIdx >= 0) {
      e.preventDefault();
      select(filtered[focusIdx]);
    }
  };

  return (
    <div className="zh-wh-selector" ref={wrapRef}>
      <button
        type="button"
        className="zh-wh-selector__trigger"
        disabled={disabled}
        aria-expanded={open}
        onClick={() => {
          setOpen((o) => !o);
        }}
      >
        <span className="material-symbols-outlined zh-wh-selector__icon">
          warehouse
        </span>
        <span className="zh-wh-selector__label">
          {selectedName || placeholder || "Seleccione bodega"}
        </span>
        <span className="material-symbols-outlined zh-wh-selector__chevron">
          expand_more
        </span>
      </button>

      {open && createPortal(
        <div className="zh-wh-selector__panel" ref={panelRef}>
          <input
            ref={inputRef}
            type="text"
            className="zh-wh-selector__search"
            placeholder="Buscar bodega..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={handleKeyDown}
          />
          <div className="zh-wh-selector__list">
            {loading ? (
              <div className="zh-wh-selector__empty">
                Cargando disponibilidad…
              </div>
            ) : filtered.length === 0 ? (
              <div className="zh-wh-selector__empty">Sin resultados</div>
            ) : (
              filtered.map((row, i) => (
                <button
                  key={row.warehouseId}
                  type="button"
                  className={`zh-wh-selector__row${i === focusIdx ? " zh-wh-selector__row--focused" : ""}${row.warehouseId === value ? " zh-wh-selector__row--selected" : ""}`}
                  onClick={() => select(row)}
                  onMouseEnter={() => setFocusIdx(i)}
                >
                  <div className="zh-wh-selector__row-name">
                    {row.warehouseId === defaultWarehouseId && (
                      <span title="Bodega por defecto">⭐</span>
                    )}
                    {row.warehouseName}
                  </div>
                  {options && (
                    <div className="zh-wh-selector__row-stock">
                      <span className="zh-wh-selector__row-metric">
                        Disp. {row.available}
                      </span>
                      <span className="zh-wh-selector__row-metric zh-wh-selector__row-metric--muted">
                        Res. {row.reserved}
                      </span>
                      <Badge
                        label={
                          row.available <= 0
                            ? "Sin stock"
                            : row.available <= 5
                              ? "Stock bajo"
                              : "Disponible"
                        }
                        variant={availabilityVariant(row.available)}
                        size="md"
                      />
                    </div>
                  )}
                </button>
              ))
            )}
          </div>
        </div>,
        document.body,
      )}
    </div>
  );
}
