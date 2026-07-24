import { useState, useEffect, useRef, useCallback } from 'react';
import { businessPartnerFacade } from '../../masterData/api/businessPartnerFacade';
import type { SupplierPickerRow } from '../../masterData/types/businessPartner.types';

type Props = {
  value: string | null;
  onChange: (supplier: SupplierPickerRow | null) => void;
  disabled?: boolean;
};

export function SupplierPicker({ value, onChange, disabled }: Props) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<SupplierPickerRow[]>([]);
  const [selected, setSelected] = useState<SupplierPickerRow | null>(null);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [focusIdx, setFocusIdx] = useState(-1);
  const wrapRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  const search = useCallback(async (q: string) => {
    if (q.trim().length < 2) { setResults([]); return; }
    setLoading(true);
    try {
      const rows = await businessPartnerFacade.searchSuppliersForPicker(q.trim());
      setResults(rows);
      setFocusIdx(-1);
    } catch { setResults([]); }
    setLoading(false);
  }, []);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    if (!open || query.length < 2) return;
    debounceRef.current = setTimeout(() => search(query), 300);
    return () => clearTimeout(debounceRef.current);
  }, [query, open, search]);

  useEffect(() => {
    if (value && !selected) {
      businessPartnerFacade.getBusinessPartner(value).then(bp => {
        const role = bp.roles?.find((r: any) => r.roleType === 'Supplier' && r.isActive);
        setSelected({
          id: bp.id,
          identificationNumber: bp.identificationNumber,
          fullName: bp.tradeName || bp.legalName,
          isActive: bp.isActive,
          hasSupplierRole: !!role,
          supplierConfig: role?.supplierConfig ?? null,
        });
      }).catch(() => {});
    }
    if (!value && selected) {
      setSelected(null);
    }
  }, [value]);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSelect = (row: SupplierPickerRow) => {
    setSelected(row);
    setOpen(false);
    setQuery('');
    onChange(row);
  };

  const handleClear = () => {
    setSelected(null);
    setQuery('');
    setResults([]);
    onChange(null);
    inputRef.current?.focus();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!open || results.length === 0) return;
    if (e.key === 'ArrowDown') { e.preventDefault(); setFocusIdx(i => Math.min(i + 1, results.length - 1)); }
    if (e.key === 'ArrowUp') { e.preventDefault(); setFocusIdx(i => Math.max(i - 1, 0)); }
    if (e.key === 'Enter' && focusIdx >= 0) { e.preventDefault(); handleSelect(results[focusIdx]); }
    if (e.key === 'Escape') { setOpen(false); }
  };

  if (selected) {
    return (
      <div style={{
        display: 'flex', alignItems: 'center', gap: 12, padding: '8px 12px',
        background: 'var(--color-surface-container-low)', border: '1px solid var(--color-border)',
        borderRadius: 'var(--radius-md)',
      }}>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--color-text-primary)' }}>
            {selected.fullName}
          </div>
          <div style={{ fontSize: 12, color: 'var(--color-text-secondary)', fontFamily: 'monospace' }}>
            {selected.identificationNumber}
          </div>
        </div>
        {!disabled && (
          <button type="button" onClick={handleClear} title="Cambiar proveedor"
            style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--color-text-secondary)', padding: 0 }}>
            <span className="material-symbols-outlined" style={{ fontSize: 18 }}>close</span>
          </button>
        )}
      </div>
    );
  }

  return (
    <div ref={wrapRef} style={{ position: 'relative' }}>
      <div style={{ position: 'relative' }}>
        <input
          ref={inputRef}
          value={query}
          onChange={e => { setQuery(e.target.value); setOpen(true); }}
          onFocus={() => { if (query.length >= 2) setOpen(true); }}
          onKeyDown={handleKeyDown}
          placeholder="Buscar por RUC, razón social o nombre..."
          disabled={disabled}
        />
        {loading && (
          <span style={{ position: 'absolute', right: 10, top: '50%', transform: 'translateY(-50%)', fontSize: 12, color: 'var(--color-text-secondary)' }}>
            Buscando...
          </span>
        )}
      </div>

      {open && query.length >= 2 && (
        <div style={{
          position: 'absolute', top: '100%', left: 0, right: 0, zIndex: 50,
          background: 'var(--color-surface)', border: '1px solid var(--color-border)',
          borderRadius: 'var(--radius-md)', boxShadow: '0 4px 12px rgba(0,0,0,0.12)',
          maxHeight: 240, overflowY: 'auto', marginTop: 4,
        }}>
          {results.length === 0 && !loading && (
            <div style={{ padding: '12px 14px', color: 'var(--color-text-secondary)', fontSize: 13, textAlign: 'center' }}>
              <div>Sin resultados para "{query}"</div>
              <div style={{ marginTop: 6 }}>
                ¿Aún no tiene proveedores registrados?{' '}
                <a href="/masterdata/suppliers" style={{ color: 'var(--color-primary)' }}>
                  Registre un proveedor
                </a>
              </div>
            </div>
          )}
          {results.map((row, i) => (
            <button key={row.id} type="button"
              onClick={() => handleSelect(row)}
              onMouseEnter={() => setFocusIdx(i)}
              style={{
                display: 'block', width: '100%', padding: '10px 14px', border: 'none', textAlign: 'left',
                cursor: 'pointer', fontSize: 13,
                background: i === focusIdx ? 'var(--color-surface-container-low)' : 'transparent',
              }}>
              <div style={{ fontWeight: 600, color: 'var(--color-text-primary)' }}>{row.fullName}</div>
              <div style={{ fontSize: 12, color: 'var(--color-text-secondary)', fontFamily: 'monospace', marginTop: 2 }}>
                {row.identificationNumber}
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
