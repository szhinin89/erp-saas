import { useCallback, useEffect, useRef, useState } from "react";
import { salesService, type SalesListItemDto } from "../api/salesService";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDate } from "../../../lib/formatters/dateFormatters";

type Props = {
  value: SalesListItemDto | null;
  onChange: (invoice: SalesListItemDto | null) => void;
  disabled?: boolean;
};

/**
 * Busca facturas autorizadas por número (única fuente válida de origen de una
 * devolución — `CreateSalesReturnDraftCommand` exige `SalesInvoiceId` de una
 * factura `Authorized`). Reutiliza `salesService.list` (mismo endpoint que la
 * pantalla de Ventas), sin crear un endpoint ni un cliente HTTP nuevo.
 */
export function SalesReturnInvoicePicker({ value, onChange, disabled }: Props) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SalesListItemDto[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  const search = useCallback(async (q: string) => {
    setLoading(true);
    try {
      const r = await salesService.list(q.trim() || undefined, "Authorized", 1, 10);
      setResults(r.items);
    } catch {
      setResults([]);
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    if (!open) return;
    debounceRef.current = setTimeout(() => void search(query), 300);
    return () => clearTimeout(debounceRef.current);
  }, [query, open, search]);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleSelect = (row: SalesListItemDto) => {
    onChange(row);
    setOpen(false);
    setQuery("");
  };

  const handleClear = () => {
    onChange(null);
    setQuery("");
    setResults([]);
  };

  if (value) {
    return (
      <div className="sr-invoice-picker-selected">
        <div className="sr-invoice-picker-selected__main">
          <span className="sr-invoice-picker-selected__number">
            {value.invoiceNumber}
          </span>
          <span className="sr-invoice-picker-selected__meta">
            {value.customerName} — {formatDate(value.issueDate)} —{" "}
            {formatMoney(value.grandTotal)}
          </span>
        </div>
        {!disabled && (
          <button
            type="button"
            className="sr-invoice-picker-selected__clear"
            onClick={handleClear}
            title="Cambiar factura"
          >
            <span className="material-symbols-outlined">close</span>
          </button>
        )}
      </div>
    );
  }

  return (
    <div ref={wrapRef} className="sr-invoice-picker">
      <input
        className="zh-input"
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        placeholder="Buscar factura autorizada por número o cliente..."
        disabled={disabled}
      />
      {open && (
        <div className="sr-invoice-picker__menu">
          {loading && <div className="sr-invoice-picker__hint">Buscando...</div>}
          {!loading && results.length === 0 && (
            <div className="sr-invoice-picker__hint">
              Sin facturas autorizadas que coincidan.
            </div>
          )}
          {!loading &&
            results.map((row) => (
              <button
                key={row.id}
                type="button"
                className="sr-invoice-picker__item"
                onClick={() => handleSelect(row)}
              >
                <span className="sr-invoice-picker__item-number">
                  {row.invoiceNumber}
                </span>
                <span className="sr-invoice-picker__item-meta">
                  {row.customerName} — {formatDate(row.issueDate)} —{" "}
                  {formatMoney(row.grandTotal)}
                </span>
              </button>
            ))}
        </div>
      )}
    </div>
  );
}
