import { useState, useEffect, useRef, useCallback } from "react";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import type { SupplierPickerRow } from "../../masterData/types/businessPartner.types";

type Props = {
  value: string | null;
  onChange: (supplier: SupplierPickerRow | null) => void;
  disabled?: boolean;
};

export function SupplierPicker({ value, onChange, disabled }: Props) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SupplierPickerRow[]>([]);
  const [selected, setSelected] = useState<SupplierPickerRow | null>(null);
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
      const rows = await businessPartnerFacade.searchSuppliersForPicker(
        q.trim(),
      );
      setResults(rows);
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
    if (value && !selected) {
      businessPartnerFacade
        .getBusinessPartner(value)
        .then((bp) => {
          const role = bp.roles?.find(
            (r: any) => r.roleType === "Supplier" && r.isActive,
          );
          setSelected({
            id: bp.id,
            identificationNumber: bp.identificationNumber,
            fullName: bp.tradeName || bp.legalName,
            isActive: bp.isActive,
            hasSupplierRole: !!role,
            supplierConfig: role?.supplierConfig ?? null,
          });
        })
        .catch(() => {});
    }
    if (!value && selected) {
      setSelected(null);
    }
  }, [value]);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node))
        setOpen(false);
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleSelect = (row: SupplierPickerRow) => {
    setSelected(row);
    setOpen(false);
    setQuery("");
    onChange(row);
  };

  const handleClear = () => {
    setSelected(null);
    setQuery("");
    setResults([]);
    onChange(null);
    inputRef.current?.focus();
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
    if (e.key === "Escape") {
      setOpen(false);
    }
  };

  if (selected) {
    return (
      <div className="zh-picker__selected">
        <div className="zh-picker__selected-body">
          <div className="zh-picker__selected-name">
            {selected.fullName}
          </div>
          <div className="zh-picker__selected-id">
            {selected.identificationNumber}
          </div>
        </div>
        {!disabled && (
          <button
            type="button"
            onClick={handleClear}
            title="Cambiar proveedor" className="zh-picker__clear"
          >
            <span
              className="material-symbols-outlined zh-picker__clear-icon"
            >
              close
            </span>
          </button>
        )}
      </div>
    );
  }

  return (
    <div ref={wrapRef} className="zh-picker">
      <div className="zh-picker__input-wrap">
        <input
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
          placeholder="Buscar por RUC, razón social o nombre..."
          disabled={disabled}
        />
        {loading && (
          <span className="zh-picker__loading">
            Buscando...
          </span>
        )}
      </div>

      {open && query.length >= 2 && (
        <div className="zh-picker__dropdown">
          {results.length === 0 && !loading && (
            <div className="zh-picker__empty">
              <div>Sin resultados para "{query}"</div>
              <div className="zh-picker__empty-help">
                ¿Aún no tiene proveedores registrados?{" "}
                <a
                  href="/masterdata/suppliers"
                  className="zh-picker__link"
                >
                  Registre un proveedor
                </a>
              </div>
            </div>
          )}
          {results.map((row, i) => (
            <button
              key={row.id}
              type="button"
              onClick={() => handleSelect(row)}
              onMouseEnter={() => setFocusIdx(i)}
              className={`zh-picker__result${i === focusIdx ? " zh-picker__result--focused" : ""}`}
            >
              <div className="zh-picker__result-name">
                {row.fullName}
              </div>
              <div className="zh-picker__result-meta">
                {row.identificationNumber}
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
