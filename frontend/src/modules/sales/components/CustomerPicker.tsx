import { useState, useEffect, useRef, useCallback } from "react";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPickerResultItem } from "../../../components/zh/ZHPickerResultItem";
import { ZHPickerSelectedValue } from "../../../components/zh/ZHPickerSelectedValue";
import type { CustomerPickerRow } from "../../masterData/types/businessPartner.types";

type Props = {
  value: string | null;
  onChange: (customer: CustomerPickerRow | null) => void;
  disabled?: boolean;
  onCreateNew?: (searchText: string) => void;
  /** Acción secundaria "editar" del cliente ya seleccionado (ver `ZHPickerSelectedValue`).
   * Sin argumentos: el consumidor ya tiene el cliente activo en su propio estado (p. ej.
   * `ctx.customerProfile` en `SalesPage`), CustomerPicker solo decide cuándo mostrar el
   * botón. Si se omite, no se renderiza la acción de editar. */
  onEditSelected?: () => void;
  /** Título/aria-label del botón editar. Default del componente global: "Editar". */
  editLabel?: string;
};

export function CustomerPicker({
  value,
  onChange,
  disabled,
  onCreateNew,
  onEditSelected,
  editLabel,
}: Props) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<CustomerPickerRow[]>([]);
  const [selected, setSelected] = useState<CustomerPickerRow | null>(null);
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
      const rows = await businessPartnerFacade.searchCustomersForPicker(
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
          setSelected({
            id: bp.id,
            identificationNumber: bp.identificationNumber,
            fullName: bp.tradeName || bp.legalName,
            isActive: bp.isActive,
            hasCustomerRole: true,
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

  const handleSelect = (row: CustomerPickerRow) => {
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
      <ZHPickerSelectedValue
        title={selected.fullName}
        meta={selected.identificationNumber}
        clearLabel="Cambiar cliente"
        editLabel={editLabel}
        onClear={disabled ? undefined : handleClear}
        onEdit={disabled ? undefined : onEditSelected}
      />
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
              <div className="zh-picker__empty-message">
                Sin resultados para &ldquo;{query}&rdquo;
              </div>
              {onCreateNew && (
                <ZHBtn
                  variant="primary"
                  size="sm"
                  onClick={() => {
                    setOpen(false);
                    onCreateNew(query);
                  }}
                >
                  <span
                    className="material-symbols-outlined zh-picker__create-icon"
                  >
                    person_add
                  </span>
                  Crear cliente
                </ZHBtn>
              )}
            </div>
          )}
          {results.map((row, i) => (
            <ZHPickerResultItem
              key={row.id}
              title={row.fullName}
              meta={row.identificationNumber}
              selected={i === focusIdx}
              onClick={() => handleSelect(row)}
              onMouseEnter={() => setFocusIdx(i)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
