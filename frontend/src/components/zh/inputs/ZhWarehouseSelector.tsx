import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Badge } from '../../PageShell';
import { stockService } from '../../../modules/inventory/stock/api/stockService';
import type { ItemWarehouseAvailabilityDto } from '../../../modules/inventory/stock/api/stockService';
import type { WarehouseDto } from '../../../modules/inventory/warehouses/api/warehouseService';
import './ZhWarehouseSelector.css';

export type { ItemWarehouseAvailabilityDto };

interface ZhWarehouseSelectorProps {
  value: string | null;
  onChange: (warehouseId: string, option?: ItemWarehouseAvailabilityDto) => void;
  /** Si se provee, consulta disponibilidad real por bodega para este item. */
  itemId?: string | null;
  /** Nombres a mostrar cuando no hay itemId, o mientras carga/si falla la consulta. */
  fallbackWarehouses: WarehouseDto[];
  /** Bodega del encabezado — se marca con ⭐ "Bodega por defecto". */
  defaultWarehouseId?: string | null;
  disabled?: boolean;
  placeholder?: string;
}

function availabilityVariant(available: number): 'green' | 'orange' | 'red' {
  if (available <= 0) return 'red';
  if (available <= 5) return 'orange';
  return 'green';
}

export function ZhWarehouseSelector({
  value, onChange, itemId, fallbackWarehouses, defaultWarehouseId, disabled, placeholder,
}: ZhWarehouseSelectorProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [focusIdx, setFocusIdx] = useState(-1);
  const [loading, setLoading] = useState(false);
  const [options, setOptions] = useState<ItemWarehouseAvailabilityDto[] | null>(null);
  const wrapRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const rows: ItemWarehouseAvailabilityDto[] = useMemo(() => {
    const base: ItemWarehouseAvailabilityDto[] = options
      ?? fallbackWarehouses.map(w => ({ warehouseId: w.id, warehouseName: w.name, available: 0, reserved: 0, canSell: true }));
    return base;
  }, [options, fallbackWarehouses]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    const list = q ? rows.filter(r => r.warehouseName.toLowerCase().includes(q)) : rows;
    if (!options) return list; // sin datos de disponibilidad: mantener orden original
    return [...list].sort((a, b) => b.available - a.available || a.warehouseName.localeCompare(b.warehouseName));
  }, [rows, query, options]);

  const selectedName =
    options?.find(o => o.warehouseId === value)?.warehouseName
    ?? fallbackWarehouses.find(w => w.id === value)?.name
    ?? '';

  useEffect(() => {
    if (!open) return;
    setQuery('');
    setFocusIdx(-1);
    if (!itemId) { setOptions(null); return; }
    let cancelled = false;
    setLoading(true);
    stockService.getWarehouseAvailability(itemId)
      .then(data => { if (!cancelled) setOptions(data); })
      .catch(() => { if (!cancelled) setOptions(null); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [open, itemId]);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const select = useCallback((row: ItemWarehouseAvailabilityDto) => {
    onChange(row.warehouseId, options ? row : undefined);
    setOpen(false);
  }, [onChange, options]);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!open || filtered.length === 0) return;
    if (e.key === 'ArrowDown') { e.preventDefault(); setFocusIdx(i => Math.min(i + 1, filtered.length - 1)); }
    if (e.key === 'ArrowUp') { e.preventDefault(); setFocusIdx(i => Math.max(i - 1, 0)); }
    if (e.key === 'Enter' && focusIdx >= 0) { e.preventDefault(); select(filtered[focusIdx]); }
    if (e.key === 'Escape') setOpen(false);
  };

  return (
    <div className="zh-wh-selector" ref={wrapRef}>
      <button type="button" className="zh-wh-selector__trigger"
        disabled={disabled}
        onClick={() => { setOpen(o => !o); setTimeout(() => inputRef.current?.focus(), 0); }}>
        <span className="material-symbols-outlined zh-wh-selector__icon">warehouse</span>
        <span className="zh-wh-selector__label">{selectedName || placeholder || 'Seleccione bodega'}</span>
        <span className="material-symbols-outlined zh-wh-selector__chevron">expand_more</span>
      </button>

      {open && (
        <div className="zh-wh-selector__panel">
          <input ref={inputRef} type="text" className="zh-wh-selector__search"
            placeholder="Buscar bodega..." value={query}
            onChange={e => setQuery(e.target.value)}
            onKeyDown={handleKeyDown} />
          <div className="zh-wh-selector__list">
            {loading ? (
              <div className="zh-wh-selector__empty">Cargando disponibilidad…</div>
            ) : filtered.length === 0 ? (
              <div className="zh-wh-selector__empty">Sin resultados</div>
            ) : filtered.map((row, i) => (
              <button key={row.warehouseId} type="button"
                className={`zh-wh-selector__row${i === focusIdx ? ' zh-wh-selector__row--focused' : ''}${row.warehouseId === value ? ' zh-wh-selector__row--selected' : ''}`}
                onClick={() => select(row)}
                onMouseEnter={() => setFocusIdx(i)}>
                <div className="zh-wh-selector__row-name">
                  {row.warehouseId === defaultWarehouseId && <span title="Bodega por defecto">⭐</span>}
                  {row.warehouseName}
                </div>
                {options && (
                  <div className="zh-wh-selector__row-stock">
                    <span className="zh-wh-selector__row-metric">Disp. {row.available}</span>
                    <span className="zh-wh-selector__row-metric zh-wh-selector__row-metric--muted">Res. {row.reserved}</span>
                    <Badge label={row.available <= 0 ? 'Sin stock' : row.available <= 5 ? 'Stock bajo' : 'Disponible'}
                      variant={availabilityVariant(row.available)} size="md" />
                  </div>
                )}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
